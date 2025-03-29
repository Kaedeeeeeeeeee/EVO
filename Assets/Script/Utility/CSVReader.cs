using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// CSV文件解析工具
/// </summary>
public class CSVReader
{
    // 分隔符
    private static readonly char[] SEPARATORS = { ',', ';', '\t' };
    private static readonly string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";

    /// <summary>
    /// 从Resources文件夹中读取CSV文件
    /// </summary>
    /// <param name="filePath">相对于Resources文件夹的路径，不需要扩展名</param>
    /// <returns>解析后的CSV数据</returns>
    public static List<Dictionary<string, string>> ReadCSVFromResources(string filePath)
    {
        TextAsset csvFile = Resources.Load<TextAsset>(filePath);
        if (csvFile == null)
        {
            Debug.LogError($"❌ 无法加载CSV文件: {filePath}");
            return new List<Dictionary<string, string>>();
        }

        return ParseCSV(csvFile.text);
    }

    /// <summary>
    /// 从指定路径读取CSV文件
    /// </summary>
    /// <param name="filePath">文件的完整路径，需要包含扩展名</param>
    /// <returns>解析后的CSV数据</returns>
    public static List<Dictionary<string, string>> ReadCSVFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"❌ CSV文件不存在: {filePath}");
            return new List<Dictionary<string, string>>();
        }

        string csvText = File.ReadAllText(filePath);
        return ParseCSV(csvText);
    }

    /// <summary>
    /// 解析CSV文本
    /// </summary>
    /// <param name="csvText">CSV文本内容</param>
    /// <returns>解析后的CSV数据</returns>
    private static List<Dictionary<string, string>> ParseCSV(string csvText)
    {
        List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();

        // 分割所有行
        string[] lines = Regex.Split(csvText, LINE_SPLIT_RE);
        if (lines.Length <= 1)
        {
            Debug.LogError("❌ CSV文件格式错误或为空!");
            return result;
        }

        // 获取第一行作为列名
        char separator = DetermineSeparator(lines[0]);
        string[] headerColumns = SplitCSVLine(lines[0], separator);

        // 处理每一行数据
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] columns = SplitCSVLine(line, separator);
            Dictionary<string, string> rowData = new Dictionary<string, string>();

            // 创建字典：列名 -> 值
            for (int j = 0; j < headerColumns.Length && j < columns.Length; j++)
            {
                string value = columns[j].Trim();
                // 移除可能的引号
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                rowData[headerColumns[j].Trim()] = value;
            }

            if (rowData.Count > 0)
            {
                result.Add(rowData);
            }
        }

        Debug.Log($"✅ 成功解析CSV数据，共{result.Count}行");
        return result;
    }

    /// <summary>
    /// 确定CSV文件使用的分隔符
    /// </summary>
    private static char DetermineSeparator(string headerLine)
    {
        foreach (char sep in SEPARATORS)
        {
            if (headerLine.Contains(sep))
            {
                return sep;
            }
        }
        return ','; // 默认使用逗号
    }

    /// <summary>
    /// 分割CSV行，正确处理引号内的分隔符
    /// </summary>
    private static string[] SplitCSVLine(string line, char separator)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentValue = "";

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                currentValue += c;
            }
            else if (c == separator && !inQuotes)
            {
                result.Add(currentValue);
                currentValue = "";
            }
            else
            {
                currentValue += c;
            }
        }

        // 添加最后一个值
        result.Add(currentValue);
        return result.ToArray();
    }
} 