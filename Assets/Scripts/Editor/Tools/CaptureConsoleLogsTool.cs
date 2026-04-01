#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键捕捉当前 Console 窗口中的全部日志到剪贴板，或保存为文本文件。
/// </summary>
public static class CaptureConsoleLogsTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsUtility + "/捕捉 Console 日志";

    [MenuItem(MenuPath, false, 200)]
    public static void CaptureToClipboard()
    {
        string text = GetConsoleLogsText();
        if (string.IsNullOrEmpty(text))
        {
            EditorUtility.DisplayDialog("捕捉 Console 日志", "当前 Console 中没有日志，或无法通过反射读取。", "确定");
            return;
        }

        GUIUtility.systemCopyBuffer = text;
        int lineCount = text.Split('\n').Length;
        EditorUtility.DisplayDialog("捕捉 Console 日志", $"已复制 {lineCount} 行到剪贴板。", "确定");
    }

    [MenuItem(MenuPath + " (另存为...)", false, 201)]
    public static void CaptureToFile()
    {
        string text = GetConsoleLogsText();
        if (string.IsNullOrEmpty(text))
        {
            EditorUtility.DisplayDialog("捕捉 Console 日志", "当前 Console 中没有日志，或无法通过反射读取。", "确定");
            return;
        }

        string path = EditorUtility.SaveFilePanel("保存 Console 日志", "Assets", "ConsoleLogs.txt", "txt");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            System.IO.File.WriteAllText(path, text, Encoding.UTF8);
            EditorUtility.DisplayDialog("捕捉 Console 日志", $"已保存到：\n{path}", "确定");
            if (path.StartsWith(Application.dataPath))
                AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("错误", $"保存失败：{ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 通过反射读取 Unity 内部 LogEntries，拼接为纯文本。
    /// </summary>
    public static string GetConsoleLogsText()
    {
        Assembly editorAssembly = Assembly.GetAssembly(typeof(Editor));
        Type logEntriesType = editorAssembly.GetType("UnityEditorInternal.LogEntries")
            ?? editorAssembly.GetType("UnityEditor.LogEntries");
        if (logEntriesType == null)
            return null;

        MethodInfo getCountMethod = logEntriesType.GetMethod("GetCount",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (getCountMethod == null)
            return null;

        int count = (int)getCountMethod.Invoke(null, null);
        if (count <= 0)
            return "";

        Type logEntryType = editorAssembly.GetType("UnityEditorInternal.LogEntry")
            ?? editorAssembly.GetType("UnityEditor.LogEntry");
        if (logEntryType == null)
            return null;

        MethodInfo getEntryMethod = GetEntryInternalMethod(logEntriesType, logEntryType);
        if (getEntryMethod == null)
            return null;

        FieldInfo messageField = logEntryType.GetField("message", BindingFlags.Public | BindingFlags.Instance)
            ?? logEntryType.GetField("message", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? logEntryType.GetField("condition", BindingFlags.Public | BindingFlags.Instance)
            ?? logEntryType.GetField("condition", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo stackTraceField = logEntryType.GetField("stackTrace", BindingFlags.Public | BindingFlags.Instance)
            ?? logEntryType.GetField("stackTrace", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo modeField = logEntryType.GetField("mode", BindingFlags.Public | BindingFlags.Instance)
            ?? logEntryType.GetField("mode", BindingFlags.NonPublic | BindingFlags.Instance);

        var sb = new StringBuilder();
        object[] args = new object[2];

        for (int i = 0; i < count; i++)
        {
            args[0] = i;
            args[1] = Activator.CreateInstance(logEntryType);

            try
            {
                getEntryMethod.Invoke(null, args);
            }
            catch
            {
                continue;
            }

            object entry = args[1];
            string message = messageField?.GetValue(entry) as string ?? "";
            string stackTrace = stackTraceField?.GetValue(entry) as string ?? "";
            int mode = modeField != null ? (int)(modeField.GetValue(entry) ?? 0) : 0;

            string prefix = GetLogPrefix(mode);
            sb.Append(prefix);
            sb.AppendLine(message);
            if (!string.IsNullOrEmpty(stackTrace))
            {
                sb.AppendLine(stackTrace);
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static MethodInfo GetEntryInternalMethod(Type logEntriesType, Type logEntryType)
    {
        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (MethodInfo m in logEntriesType.GetMethods(flags))
        {
            if (m.Name != "GetEntryInternal")
                continue;
            var ps = m.GetParameters();
            if (ps.Length != 2 || ps[0].ParameterType != typeof(int))
                continue;
            Type second = ps[1].ParameterType;
            if (second.IsByRef)
                second = second.GetElementType();
            if (second == logEntryType)
                return m;
        }
        return null;
    }

    private static string GetLogPrefix(int mode)
    {
        const int Error = 1;
        const int Warning = 2;
        if ((mode & Error) != 0 || (mode & 4) != 0)
            return "[Error] ";
        if ((mode & Warning) != 0)
            return "[Warning] ";
        return "[Log] ";
    }
}
#endif
