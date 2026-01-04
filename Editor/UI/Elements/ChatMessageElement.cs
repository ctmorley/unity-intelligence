using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityAIAssistant.Editor.UI.Elements
{
    /// <summary>
    /// Visual element for rendering a chat message with markdown support.
    /// </summary>
    public class ChatMessageElement : VisualElement
    {
        private readonly ChatRole role;
        private readonly VisualElement contentContainer;
        private readonly StringBuilder streamingContent;
        private bool isStreaming;

        private static readonly Color UserBgColor = new Color(0.2f, 0.3f, 0.4f, 0.5f);
        private static readonly Color AssistantBgColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
        private static readonly Color SystemBgColor = new Color(0.4f, 0.3f, 0.2f, 0.5f);

        public ChatMessageElement(ChatRole role, string content)
        {
            this.role = role;
            streamingContent = new StringBuilder();

            // Container styling
            style.marginBottom = 10;
            style.paddingLeft = 12;
            style.paddingRight = 12;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.borderTopLeftRadius = 8;
            style.borderTopRightRadius = 8;
            style.borderBottomLeftRadius = 8;
            style.borderBottomRightRadius = 8;

            // Role-based colors
            switch (role)
            {
                case ChatRole.User:
                    style.backgroundColor = UserBgColor;
                    break;
                case ChatRole.Assistant:
                    style.backgroundColor = AssistantBgColor;
                    break;
                case ChatRole.System:
                    style.backgroundColor = SystemBgColor;
                    break;
            }

            // Role label
            var roleLabel = new Label(GetRoleLabel(role));
            roleLabel.style.fontSize = 10;
            roleLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            roleLabel.style.marginBottom = 6;
            roleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(roleLabel);

            // Content container
            contentContainer = new VisualElement();
            Add(contentContainer);

            if (!string.IsNullOrEmpty(content))
            {
                RenderContent(content);
            }
        }

        private string GetRoleLabel(ChatRole role)
        {
            return role switch
            {
                ChatRole.User => "YOU",
                ChatRole.Assistant => "ASSISTANT",
                ChatRole.System => "SYSTEM",
                _ => "UNKNOWN"
            };
        }

        public void BeginStreaming()
        {
            isStreaming = true;
            streamingContent.Clear();
        }

        public void AppendText(string chunk)
        {
            if (!isStreaming)
            {
                BeginStreaming();
            }

            streamingContent.Append(chunk);
            RenderContent(streamingContent.ToString());
        }

        public void EndStreaming()
        {
            isStreaming = false;
        }

        private void RenderContent(string content)
        {
            contentContainer.Clear();

            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            // Parse markdown - simplified version
            var segments = ParseMarkdown(content);

            foreach (var segment in segments)
            {
                VisualElement element;

                switch (segment.Type)
                {
                    case SegmentType.Code:
                        element = CreateCodeBlock(segment.Content, segment.Language);
                        break;

                    case SegmentType.InlineCode:
                        element = CreateInlineCode(segment.Content);
                        break;

                    default:
                        element = CreateTextBlock(segment.Content);
                        break;
                }

                contentContainer.Add(element);
            }
        }

        private VisualElement CreateTextBlock(string text)
        {
            var label = new Label(text);
            label.style.color = Color.white;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            return label;
        }

        private VisualElement CreateCodeBlock(string code, string language)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.marginTop = 8;
            container.style.marginBottom = 8;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;

            // Header with language and copy button
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 6;

            var langLabel = new Label(string.IsNullOrEmpty(language) ? "code" : language);
            langLabel.style.fontSize = 10;
            langLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            header.Add(langLabel);

            var copyButton = new Button(() => CopyToClipboard(code)) { text = "Copy" };
            copyButton.style.fontSize = 10;
            copyButton.style.paddingLeft = 8;
            copyButton.style.paddingRight = 8;
            copyButton.style.paddingTop = 2;
            copyButton.style.paddingBottom = 2;
            header.Add(copyButton);

            container.Add(header);

            // Code content
            var codeLabel = new Label(code);
            codeLabel.style.color = new Color(0.8f, 0.9f, 0.8f);
            codeLabel.style.whiteSpace = WhiteSpace.Pre;
            codeLabel.style.fontSize = 12;
            codeLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            codeLabel.selection.isSelectable = true;
            container.Add(codeLabel);

            return container;
        }

        private VisualElement CreateInlineCode(string code)
        {
            var label = new Label(code);
            label.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            label.style.color = new Color(0.9f, 0.7f, 0.5f);
            label.style.paddingLeft = 4;
            label.style.paddingRight = 4;
            label.style.paddingTop = 1;
            label.style.paddingBottom = 1;
            label.style.borderTopLeftRadius = 3;
            label.style.borderTopRightRadius = 3;
            label.style.borderBottomLeftRadius = 3;
            label.style.borderBottomRightRadius = 3;
            label.style.fontSize = 11;
            return label;
        }

        private void CopyToClipboard(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }

        #region Markdown Parsing

        private enum SegmentType { Text, Code, InlineCode }

        private class Segment
        {
            public SegmentType Type;
            public string Content;
            public string Language;
        }

        private static readonly Regex CodeBlockRegex = new Regex(
            @"```(\w*)\n?([\s\S]*?)```",
            RegexOptions.Compiled);

        private static readonly Regex InlineCodeRegex = new Regex(
            @"`([^`]+)`",
            RegexOptions.Compiled);

        private System.Collections.Generic.List<Segment> ParseMarkdown(string content)
        {
            var segments = new System.Collections.Generic.List<Segment>();
            int lastIndex = 0;

            // Find code blocks
            var codeBlocks = CodeBlockRegex.Matches(content);
            foreach (Match match in codeBlocks)
            {
                // Text before code block
                if (match.Index > lastIndex)
                {
                    string textBefore = content.Substring(lastIndex, match.Index - lastIndex);
                    AddTextWithInlineCode(segments, textBefore);
                }

                // Code block
                segments.Add(new Segment
                {
                    Type = SegmentType.Code,
                    Language = match.Groups[1].Value,
                    Content = match.Groups[2].Value.Trim()
                });

                lastIndex = match.Index + match.Length;
            }

            // Remaining text
            if (lastIndex < content.Length)
            {
                string remaining = content.Substring(lastIndex);
                AddTextWithInlineCode(segments, remaining);
            }

            return segments;
        }

        private void AddTextWithInlineCode(System.Collections.Generic.List<Segment> segments, string text)
        {
            int lastIndex = 0;
            var inlineMatches = InlineCodeRegex.Matches(text);

            foreach (Match match in inlineMatches)
            {
                if (match.Index > lastIndex)
                {
                    segments.Add(new Segment
                    {
                        Type = SegmentType.Text,
                        Content = text.Substring(lastIndex, match.Index - lastIndex)
                    });
                }

                segments.Add(new Segment
                {
                    Type = SegmentType.InlineCode,
                    Content = match.Groups[1].Value
                });

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                segments.Add(new Segment
                {
                    Type = SegmentType.Text,
                    Content = text.Substring(lastIndex)
                });
            }
        }

        #endregion
    }
}
