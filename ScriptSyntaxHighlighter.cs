using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace ReAgent;

internal static class ScriptSyntaxHighlighter
{
    private enum TokenKind
    {
        Text,
        Whitespace,
        Identifier,
        Keyword,
        Type,
        Api,
        Variable,
        Number,
        String,
        Comment,
        Operator
    }

    private readonly record struct Token(string Text, TokenKind Kind);

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "base", "break", "case", "catch", "checked", "class", "const", "continue", "default",
        "delegate", "do", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed",
        "for", "foreach", "if", "implicit", "in", "interface", "internal", "is", "lock", "namespace",
        "new", "null", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sizeof", "stackalloc", "static", "switch", "this", "throw",
        "true", "try", "typeof", "unchecked", "unsafe", "using", "virtual", "void", "volatile",
        "while", "var", "when"
    };

    private static readonly HashSet<string> TypeNames = new(StringComparer.Ordinal)
    {
        "bool", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long", "object",
        "sbyte", "short", "string", "uint", "ulong", "ushort",
        "Vector2", "Vector3", "Vector4", "Color", "Keys",
        "List", "Dictionary", "HashSet", "Queue", "IEnumerable", "IList", "IReadOnlyList",
        "RuleState", "ISideEffect", "PressKeySideEffect", "SetNumberSideEffect", "ResetNumberSideEffect",
        "SetFlagSideEffect", "ResetFlagSideEffect", "StartTimerSideEffect", "StopTimerSideEffect",
        "RestartTimerSideEffect", "ResetTimerSideEffect", "DisplayTextSideEffect", "DisplayGraphicSideEffect",
        "ProgressBarSideEffect", "ChronomancerPendulumSideEffect", "DelayedSideEffect", "PluginBridgeSideEffect",
        "FlaskInfo", "FlasksInfo", "VitalsInfo", "Vital", "BuffDictionary", "StatDictionary", "SkillDictionary"
    };

    private static readonly HashSet<string> ApiNames = new(StringComparer.Ordinal)
    {
        "State", "Math", "Enumerable", "DateTime", "TimeSpan", "StringComparison", "StringSplitOptions"
    };

    private static readonly Vector4 DefaultColor = new(0.86f, 0.86f, 0.86f, 1f);
    private static readonly Vector4 KeywordColor = new(0.35f, 0.62f, 0.95f, 1f);
    private static readonly Vector4 TypeColor = new(0.33f, 0.78f, 0.82f, 1f);
    private static readonly Vector4 ApiColor = new(0.50f, 0.85f, 1.00f, 1f);
    private static readonly Vector4 VariableColor = new(1.00f, 0.82f, 0.36f, 1f);
    private static readonly Vector4 NumberColor = new(0.76f, 0.63f, 1.00f, 1f);
    private static readonly Vector4 StringColor = new(0.71f, 0.89f, 0.50f, 1f);
    private static readonly Vector4 CommentColor = new(0.50f, 0.56f, 0.61f, 1f);
    private static readonly Vector4 OperatorColor = new(0.78f, 0.78f, 0.78f, 1f);

    public static void DrawPreview(string id, string source)
    {
        var lines = SplitLines(source);
        var tokenizedLines = new List<List<Token>>(lines.Length);
        var allTokens = new List<Token>();

        foreach (var line in lines)
        {
            var tokens = TokenizeLine(line);
            tokenizedLines.Add(tokens);
            allTokens.AddRange(tokens);
        }

        var variableNames = CollectVariableNames(allTokens);
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        var style = ImGui.GetStyle();
        var desiredHeight = Math.Clamp((lines.Length + 1) * lineHeight + style.FramePadding.Y * 2f, lineHeight * 3f, lineHeight * 14f);

        ImGui.TextDisabled("Syntax preview (modern)");
        ImGui.BeginChild(id, new Vector2(ImGui.GetContentRegionAvail().X, desiredHeight), ImGuiChildFlags.Border, ImGuiWindowFlags.HorizontalScrollbar);
        try
        {
            var drawList = ImGui.GetWindowDrawList();
            var spaceWidth = ImGui.CalcTextSize(" ").X;

            foreach (var lineTokens in tokenizedLines)
            {
                var cursor = ImGui.GetCursorScreenPos();
                var x = cursor.X;

                foreach (var token in lineTokens)
                {
                    var text = token.Text.Replace("\t", "    ", StringComparison.Ordinal);
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (token.Kind == TokenKind.Whitespace)
                    {
                        x += text.Length * spaceWidth;
                        continue;
                    }

                    var kind = token.Kind == TokenKind.Identifier && variableNames.Contains(token.Text)
                        ? TokenKind.Variable
                        : token.Kind;

                    drawList.AddText(new Vector2(x, cursor.Y), ImGui.ColorConvertFloat4ToU32(GetColor(kind)), text);
                    x += ImGui.CalcTextSize(text).X;
                }

                ImGui.Dummy(new Vector2(Math.Max(1f, x - cursor.X), lineHeight));
            }
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private static string[] SplitLines(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return [string.Empty];
        }

        return source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static List<Token> TokenizeLine(string line)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < line.Length)
        {
            var current = line[index];

            if (char.IsWhiteSpace(current))
            {
                var start = index++;
                while (index < line.Length && char.IsWhiteSpace(line[index]))
                {
                    index++;
                }

                tokens.Add(new Token(line[start..index], TokenKind.Whitespace));
                continue;
            }

            if (current == '/' && index + 1 < line.Length)
            {
                var next = line[index + 1];
                if (next == '/')
                {
                    tokens.Add(new Token(line[index..], TokenKind.Comment));
                    break;
                }

                if (next == '*')
                {
                    var end = line.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    end = end < 0 ? line.Length : end + 2;
                    tokens.Add(new Token(line[index..end], TokenKind.Comment));
                    index = end;
                    continue;
                }
            }

            if (current is '"' or '\'' || IsPrefixedStringStart(line, index))
            {
                var end = ReadString(line, index);
                tokens.Add(new Token(line[index..end], TokenKind.String));
                index = end;
                continue;
            }

            if (char.IsDigit(current))
            {
                var start = index++;
                while (index < line.Length && IsNumberPart(line[index]))
                {
                    index++;
                }

                tokens.Add(new Token(line[start..index], TokenKind.Number));
                continue;
            }

            if (IsIdentifierStart(current))
            {
                var start = index++;
                while (index < line.Length && IsIdentifierPart(line[index]))
                {
                    index++;
                }

                var text = line[start..index];
                tokens.Add(new Token(text, GetIdentifierKind(text)));
                continue;
            }

            tokens.Add(new Token(line[index..++index], TokenKind.Operator));
        }

        return tokens;
    }

    private static HashSet<string> CollectVariableNames(List<Token> tokens)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var significant = new List<Token>();

        foreach (var token in tokens)
        {
            if (token.Kind is not TokenKind.Whitespace and not TokenKind.Comment)
            {
                significant.Add(token);
            }
        }

        for (var i = 0; i < significant.Count; i++)
        {
            if (significant[i].Text == "foreach")
            {
                CollectForeachVariable(significant, i, names);
                continue;
            }

            if (!TryReadDeclaration(significant, i, out var nameIndex))
            {
                continue;
            }

            AddDeclarationNames(significant, nameIndex, names);
        }

        return names;
    }

    private static void CollectForeachVariable(List<Token> tokens, int foreachIndex, HashSet<string> names)
    {
        for (var i = foreachIndex + 1; i < tokens.Count && tokens[i].Text != ")"; i++)
        {
            if (TryReadDeclaration(tokens, i, out var nameIndex) && nameIndex + 1 < tokens.Count && tokens[nameIndex + 1].Text == "in")
            {
                names.Add(tokens[nameIndex].Text);
                return;
            }
        }
    }

    private static bool TryReadDeclaration(List<Token> tokens, int index, out int nameIndex)
    {
        nameIndex = -1;
        var current = index;

        if (tokens[current].Text == "const")
        {
            current++;
            if (current >= tokens.Count)
            {
                return false;
            }
        }

        if (tokens[current].Text == "var")
        {
            nameIndex = current + 1;
            return IsValidDeclaredName(tokens, nameIndex);
        }

        if (!IsTypeToken(tokens[current]))
        {
            return false;
        }

        var afterType = ReadType(tokens, current);
        nameIndex = afterType;
        return IsValidDeclaredName(tokens, nameIndex);
    }

    private static int ReadType(List<Token> tokens, int index)
    {
        var current = index + 1;

        while (current + 1 < tokens.Count && tokens[current].Text == "." && IsTypeToken(tokens[current + 1]))
        {
            current += 2;
        }

        if (current < tokens.Count && tokens[current].Text == "<")
        {
            var depth = 0;
            while (current < tokens.Count)
            {
                if (tokens[current].Text == "<")
                {
                    depth++;
                }
                else if (tokens[current].Text == ">")
                {
                    depth--;
                    if (depth == 0)
                    {
                        current++;
                        break;
                    }
                }

                current++;
            }
        }

        while (current < tokens.Count && tokens[current].Text is "?" or "[" or "]")
        {
            current++;
        }

        return current;
    }

    private static bool IsValidDeclaredName(List<Token> tokens, int index)
    {
        if (index < 0 || index >= tokens.Count || tokens[index].Kind != TokenKind.Identifier)
        {
            return false;
        }

        return index + 1 >= tokens.Count || tokens[index + 1].Text is "=" or ";" or "," or ")" or "in";
    }

    private static void AddDeclarationNames(List<Token> tokens, int firstNameIndex, HashSet<string> names)
    {
        names.Add(tokens[firstNameIndex].Text);

        for (var i = firstNameIndex + 1; i < tokens.Count && tokens[i].Text != ";"; i++)
        {
            if (tokens[i].Text == "," && i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Identifier)
            {
                names.Add(tokens[i + 1].Text);
            }
        }
    }

    private static bool IsPrefixedStringStart(string line, int index)
    {
        if (line[index] == '@' && index + 1 < line.Length && line[index + 1] == '"')
        {
            return true;
        }

        if (line[index] != '$' || index + 1 >= line.Length)
        {
            return false;
        }

        return line[index + 1] == '"' || line[index + 1] == '@' && index + 2 < line.Length && line[index + 2] == '"';
    }

    private static int ReadString(string line, int start)
    {
        var index = start;
        var verbatim = false;

        if (line[index] == '$')
        {
            index++;
        }

        if (index < line.Length && line[index] == '@')
        {
            verbatim = true;
            index++;
        }

        if (index >= line.Length)
        {
            return line.Length;
        }

        var quote = line[index++];
        while (index < line.Length)
        {
            if (verbatim && quote == '"' && line[index] == '"' && index + 1 < line.Length && line[index + 1] == '"')
            {
                index += 2;
                continue;
            }

            if (!verbatim && line[index] == '\\')
            {
                index += Math.Min(2, line.Length - index);
                continue;
            }

            if (line[index++] == quote)
            {
                break;
            }
        }

        return index;
    }

    private static TokenKind GetIdentifierKind(string text)
    {
        if (TypeNames.Contains(text))
        {
            return TokenKind.Type;
        }

        if (ApiNames.Contains(text))
        {
            return TokenKind.Api;
        }

        return Keywords.Contains(text) ? TokenKind.Keyword : TokenKind.Identifier;
    }

    private static bool IsTypeToken(Token token)
    {
        return token.Kind == TokenKind.Type;
    }

    private static bool IsIdentifierStart(char value)
    {
        return value == '_' || char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value)
    {
        return value == '_' || char.IsLetterOrDigit(value);
    }

    private static bool IsNumberPart(char value)
    {
        return char.IsLetterOrDigit(value) || value is '.' or '_';
    }

    private static Vector4 GetColor(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Keyword => KeywordColor,
            TokenKind.Type => TypeColor,
            TokenKind.Api => ApiColor,
            TokenKind.Variable => VariableColor,
            TokenKind.Number => NumberColor,
            TokenKind.String => StringColor,
            TokenKind.Comment => CommentColor,
            TokenKind.Operator => OperatorColor,
            _ => DefaultColor
        };
    }
}
