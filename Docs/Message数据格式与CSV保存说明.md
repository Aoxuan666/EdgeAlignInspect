# Message 数据格式与 CSV 保存说明

本文档说明 `TemplateEdgeInspectSdk.RunInspection(...)` 返回的 `EdgeInspectResult.Message` 字符串格式，以及上位机如何解析并保存 CSV。

## 1. 推荐读取方式

如果上位机是直接引用 SDK，推荐优先读取结构化对象字段，不要只解析 `Message`：

- 总结果：`result.Success`、`result.NgReasons`、`result.NgReasonText`、`result.Message`
- ROI 结果：`result.DetectResults`
- 点结果：`roi.Points`

`Message` 是给上位机快速显示和简单汇总用的摘要字符串，不是 JSON。

## 2. Message 正常格式

正常完成检测后，`Message` 格式如下：

```text
总体=OK/NG,毛刺公差=0.0000mm,凹陷公差=0.0000mm;ROI名称=OK/NG,最大毛刺=0.0000mm,最大凹陷=0.0000mm;ROI名称=OK/NG,最大毛刺=0.0000mm,最大凹陷=0.0000mm
```

示例：

```text
总体=NG,毛刺公差=0.0500mm,凹陷公差=0.0500mm;检测1=OK,最大毛刺=0.0123mm,最大凹陷=0.0000mm;检测2=NG,最大毛刺=0.0789mm,最大凹陷=0.0100mm
```

分隔规则：

- `;` 分隔总结果和每个 ROI 的结果段。
- 每段内部用 `,` 分隔字段。
- 每个字段用 `=` 分隔字段名和字段值。
- 第一段固定是总结果，包含 `总体`、`毛刺公差`、`凹陷公差`。
- 后续每段是一个检测 ROI，第一项的字段名就是 ROI 名称，字段值是 `OK` 或 `NG`。
- 通过 SDK 的 `RunInspection(..., EdgeInspectionToleranceOptions options)` 调用时，单位通常是 `mm`。
- 如果绕过 SDK 直接调用处理器并使用本地像素公差，单位可能是 `px`。

注意：ROI 名称不要包含 `;`、`,`、`=`，否则字符串解析会歧义。

## 3. 异常或参数错误格式

如果输入参数错误或算法异常，SDK 可能直接返回普通错误文本，例如：

```text
输入图像为空。
Job 参数为空。
公差参数为空。
算法异常：...
```

所以上位机解析前应先判断 `Message` 是否包含 `总体=`。不包含时按错误文本保存即可。

## 4. Message 解析示例

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public sealed class ParsedMessage
{
    public string OverallStatus { get; set; } = "";
    public double BurrTolerance { get; set; }
    public double DentTolerance { get; set; }
    public string Unit { get; set; } = "";
    public string RawMessage { get; set; } = "";
    public List<ParsedRoiMessage> Rois { get; } = new List<ParsedRoiMessage>();
}

public sealed class ParsedRoiMessage
{
    public string RoiName { get; set; } = "";
    public string Status { get; set; } = "";
    public double MaxBurr { get; set; }
    public double MaxDent { get; set; }
}

public static ParsedMessage ParseMessage(string message)
{
    var result = new ParsedMessage { RawMessage = message ?? "" };
    if (string.IsNullOrWhiteSpace(message) || !message.Contains("总体="))
    {
        return result;
    }

    string[] sections = message.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
    if (sections.Length == 0)
    {
        return result;
    }

    Dictionary<string, string> total = ParseKeyValues(sections[0]);
    result.OverallStatus = GetValue(total, "总体");
    result.BurrTolerance = ParseNumberAndUnit(GetValue(total, "毛刺公差"), out string unit);
    result.DentTolerance = ParseNumberAndUnit(GetValue(total, "凹陷公差"), out _);
    result.Unit = unit;

    for (int i = 1; i < sections.Length; i++)
    {
        string[] fields = sections[i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0)
        {
            continue;
        }

        string[] first = fields[0].Split(new[] { '=' }, 2);
        if (first.Length != 2)
        {
            continue;
        }

        var roi = new ParsedRoiMessage
        {
            RoiName = first[0].Trim(),
            Status = first[1].Trim()
        };

        Dictionary<string, string> kv = ParseKeyValues(string.Join(",", fields.Skip(1)));
        roi.MaxBurr = ParseNumberAndUnit(GetValue(kv, "最大毛刺"), out _);
        roi.MaxDent = ParseNumberAndUnit(GetValue(kv, "最大凹陷"), out _);
        result.Rois.Add(roi);
    }

    return result;
}

private static Dictionary<string, string> ParseKeyValues(string section)
{
    var dict = new Dictionary<string, string>();
    foreach (string item in (section ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
    {
        string[] pair = item.Split(new[] { '=' }, 2);
        if (pair.Length == 2)
        {
            dict[pair[0].Trim()] = pair[1].Trim();
        }
    }
    return dict;
}

private static string GetValue(Dictionary<string, string> dict, string key)
{
    return dict.TryGetValue(key, out string value) ? value : "";
}

private static double ParseNumberAndUnit(string text, out string unit)
{
    unit = "";
    if (string.IsNullOrWhiteSpace(text))
    {
        return 0;
    }

    string value = text.Trim();
    if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
    {
        unit = "mm";
        value = value.Substring(0, value.Length - 2);
    }
    else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
    {
        unit = "px";
        value = value.Substring(0, value.Length - 2);
    }

    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
        ? number
        : 0;
}
```

## 5. CSV 保存建议

建议保存两张 CSV，避免一行里塞太多点数据。

### 5.1 每次检测汇总 CSV

一张图或一次检测保存一行：

```text
Time,Barcode,Success,NgReasons,Message,BurrTolerance,DentTolerance,Unit,RoiCount,BurrCount,DentCount,OverEdgeCount,CopperLeakCount
```

字段来源：

- `Success`：`result.Success`
- `NgReasons`：`result.NgReasons`
- `Message`：`result.Message`
- `BurrTolerance` / `DentTolerance` / `Unit`：可从 `Message` 解析，也可直接读取 `result.ExternalBurrTolerance`、`result.ExternalDentTolerance`
- `RoiCount`：`result.DetectResults.Count`
- `BurrCount` / `DentCount` / `OverEdgeCount` / `CopperLeakCount`：直接读取 `result` 对象字段

### 5.2 每个 ROI 明细 CSV

每个检测 ROI 保存一行：

```text
Time,Barcode,RoiIndex,RoiName,RoiSuccess,NgReasons,Message,MaxBurr,MaxDent,Unit,BurrCount,DentCount,HasOverallDistance,OverallDistanceValue,OverallDeltaValue,IsOverEdge,IsCopperLeak
```

字段来源：

- `RoiIndex`：`roi.Index`
- `RoiName`：`roi.Name`
- `RoiSuccess`：`roi.Success`
- `NgReasons`：`roi.NgReasons`
- `Message`：`roi.Message`
- `MaxBurr` / `MaxDent` / `Unit`：可从 `Message` 的 ROI 段解析，或者遍历 `roi.Points` 计算
- `BurrCount` / `DentCount`：`roi.BurrCount`、`roi.DentCount`
- `HasOverallDistance`：`roi.HasOverallDistance`
- `OverallDistanceValue`：`roi.OverallDistanceValue`
- `OverallDeltaValue`：`roi.OverallDeltaValue`
- `IsOverEdge`：`roi.IsOverEdge`
- `IsCopperLeak`：`roi.IsCopperLeak`

## 6. CSV 写入示例

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public static void AppendResultCsv(string summaryCsv, string roiCsv, string barcode, EdgeInspectResult result)
{
    DateTime now = DateTime.Now;
    ParsedMessage parsed = ParseMessage(result?.Message);

    bool writeSummaryHeader = !File.Exists(summaryCsv);
    using (var writer = new StreamWriter(summaryCsv, true, Encoding.UTF8))
    {
        if (writeSummaryHeader)
        {
            writer.WriteLine("Time,Barcode,Success,NgReasons,Message,BurrTolerance,DentTolerance,Unit,RoiCount,BurrCount,DentCount,OverEdgeCount,CopperLeakCount");
        }

        writer.WriteLine(string.Join(",", new[]
        {
            Csv(now.ToString("yyyy-MM-dd HH:mm:ss.fff")),
            Csv(barcode),
            Csv(result?.Success.ToString() ?? ""),
            Csv(result?.NgReasons.ToString() ?? ""),
            Csv(result?.Message ?? ""),
            Num(parsed.BurrTolerance),
            Num(parsed.DentTolerance),
            Csv(parsed.Unit),
            Num(result?.DetectResults.Count ?? 0),
            Num(result?.BurrCount ?? 0),
            Num(result?.DentCount ?? 0),
            Num(result?.OverEdgeCount ?? 0),
            Num(result?.CopperLeakCount ?? 0)
        }));
    }

    bool writeRoiHeader = !File.Exists(roiCsv);
    using (var writer = new StreamWriter(roiCsv, true, Encoding.UTF8))
    {
        if (writeRoiHeader)
        {
            writer.WriteLine("Time,Barcode,RoiIndex,RoiName,RoiSuccess,NgReasons,Message,MaxBurr,MaxDent,Unit,BurrCount,DentCount,HasOverallDistance,OverallDistanceValue,OverallDeltaValue,IsOverEdge,IsCopperLeak");
        }

        foreach (DetectRoiInspectResult roi in result?.DetectResults ?? Enumerable.Empty<DetectRoiInspectResult>())
        {
            ParsedRoiMessage parsedRoi = parsed.Rois.FirstOrDefault(x => x.RoiName == roi.Name);
            writer.WriteLine(string.Join(",", new[]
            {
                Csv(now.ToString("yyyy-MM-dd HH:mm:ss.fff")),
                Csv(barcode),
                Num(roi.Index),
                Csv(roi.Name),
                Csv(roi.Success.ToString()),
                Csv(roi.NgReasons.ToString()),
                Csv(roi.Message),
                Num(parsedRoi?.MaxBurr ?? 0),
                Num(parsedRoi?.MaxDent ?? 0),
                Csv(parsed.Unit),
                Num(roi.BurrCount),
                Num(roi.DentCount),
                Csv(roi.HasOverallDistance.ToString()),
                Num(roi.OverallDistanceValue),
                Num(roi.OverallDeltaValue),
                Csv(roi.IsOverEdge.ToString()),
                Csv(roi.IsCopperLeak.ToString())
            }));
        }
    }
}

private static string Csv(string value)
{
    value = value ?? "";
    return "\"" + value.Replace("\"", "\"\"") + "\"";
}

private static string Num(double value)
{
    return value.ToString("0.####", CultureInfo.InvariantCulture);
}

private static string Num(int value)
{
    return value.ToString(CultureInfo.InvariantCulture);
}
```

## 7. 最小对接结论

上位机只需要简单显示和保存时，解析 `Message` 即可。

上位机需要稳定拿 NG 类型、ROI 名称、点位、整体距离、毛刺/凹陷/超边/漏铜等详细数据时，应直接读取 `EdgeInspectResult`、`DetectRoiInspectResult` 和 `EdgePointResult` 字段，再保存 CSV。
