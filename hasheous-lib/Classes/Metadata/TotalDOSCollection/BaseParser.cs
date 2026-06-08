using System;
using System.IO;
using System.Text;
using gaseous_signature_parser.models.RomSignatureObject;

namespace TotalDOSCollection
{
    public abstract class BaseParser
    {
        protected sealed class TopLevelBlock
        {
            public string Tag { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        private enum TokenType
        {
            Word,
            Quoted,
            OpenParen,
            CloseParen
        }

        private sealed class Token
        {
            public TokenType Type { get; set; }
            public string Value { get; set; } = string.Empty;
        }

        protected Dictionary<string, string> ExtractHeaderData(string datFile, string? headerTag = null)
        {
            Dictionary<string, string> headerData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<TopLevelBlock> blocks = ReadTopLevelBlocks(datFile);

            if (blocks.Count == 0)
            {
                return headerData;
            }

            TopLevelBlock? headerBlock;
            if (string.IsNullOrWhiteSpace(headerTag))
            {
                headerBlock = blocks[0];
            }
            else
            {
                headerBlock = blocks.FirstOrDefault(block => string.Equals(block.Tag, headerTag, StringComparison.OrdinalIgnoreCase));
            }

            if (headerBlock == null)
            {
                return headerData;
            }

            using (StringReader lineReader = new StringReader(headerBlock.Content))
            {
                string? line;
                while ((line = lineReader.ReadLine()) != null)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        continue;
                    }

                    int separatorIndex = trimmedLine.IndexOf(':');
                    if (separatorIndex > 0)
                    {
                        string key = trimmedLine.Substring(0, separatorIndex).Trim();
                        string value = trimmedLine.Substring(separatorIndex + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            headerData[key] = value;
                        }
                        continue;
                    }

                    List<Token> lineTokens = Tokenize(trimmedLine);
                    if (lineTokens.Count >= 2 && lineTokens[0].Type == TokenType.Word)
                    {
                        string key = lineTokens[0].Value;
                        string value = string.Join(" ", lineTokens.Skip(1).Select(token => token.Value));
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            headerData[key] = value;
                        }
                    }
                }
            }

            return headerData;
        }

        protected List<Dictionary<string, object>> ExtractDataEntries(string datFile, IEnumerable<string> entryTags, IEnumerable<string>? childTags = null)
        {
            List<Dictionary<string, object>> dataEntries = new List<Dictionary<string, object>>();
            List<TopLevelBlock> blocks = ReadTopLevelBlocks(datFile);

            HashSet<string> entryTagSet = new HashSet<string>(entryTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string>? childTagSet = null;
            if (childTags != null)
            {
                childTagSet = new HashSet<string>(childTags, StringComparer.OrdinalIgnoreCase);
            }

            if (entryTagSet.Count == 0)
            {
                return dataEntries;
            }

            foreach (TopLevelBlock block in blocks)
            {
                if (!entryTagSet.Contains(block.Tag))
                {
                    continue;
                }

                List<Token> blockTokens = Tokenize(block.Content);
                Dictionary<string, object> entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tag"] = block.Tag,
                    ["attributes"] = ParseAttributes(blockTokens, 0, blockTokens.Count)
                };

                List<Dictionary<string, object>> children = ParseChildrenFromLines(block.Content, childTagSet);

                if (children.Count == 0)
                {
                    int tokenIndex = 0;
                    while (tokenIndex < blockTokens.Count - 1)
                    {
                        if (blockTokens[tokenIndex].Type == TokenType.Word && blockTokens[tokenIndex + 1].Type == TokenType.OpenParen)
                        {
                            string childTag = blockTokens[tokenIndex].Value;
                            int start = tokenIndex + 2;
                            int depth = 1;
                            int current = start;

                            while (current < blockTokens.Count && depth > 0)
                            {
                                if (blockTokens[current].Type == TokenType.OpenParen)
                                {
                                    depth++;
                                }
                                else if (blockTokens[current].Type == TokenType.CloseParen)
                                {
                                    depth--;
                                }

                                current++;
                            }

                            int endExclusive = Math.Max(start, current - 1);

                            bool includeChild = childTagSet == null || childTagSet.Count == 0 || childTagSet.Contains(childTag);
                            if (!includeChild)
                            {
                                tokenIndex = current;
                                continue;
                            }

                            Dictionary<string, object> child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["tag"] = childTag,
                                ["attributes"] = ParseAttributes(blockTokens, start, endExclusive)
                            };

                            children.Add(child);
                            tokenIndex = current;
                            continue;
                        }

                        tokenIndex++;
                    }
                }

                entry["children"] = children;
                dataEntries.Add(entry);
            }

            return dataEntries;
        }

        /// <summary>
        /// Streams raw entry blocks from a DAT file one at a time without loading the whole file into memory.
        /// Only blocks whose tag matches <paramref name="entryTags"/> are yielded.
        /// Each yielded tuple contains the block tag and the raw content string between the outer parentheses.
        /// </summary>
        protected IEnumerable<(string Tag, string Content)> StreamRawEntryBlocks(string datFile, IEnumerable<string> entryTags)
        {
            HashSet<string> entryTagSet = new HashSet<string>(entryTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            using StreamReader reader = new StreamReader(datFile);
            StringBuilder outsideBuffer = new StringBuilder();
            StringBuilder contentBuffer = new StringBuilder();

            bool inQuotedString = false;
            bool isEscaped = false;
            bool hasPreviousChar = false;
            char previousChar = '\0';
            int depth = 0;
            string? currentTag = null;

            int value;
            while ((value = reader.Read()) != -1)
            {
                char currentChar = (char)value;
                int nextValue = reader.Peek();
                char? nextChar = nextValue == -1 ? (char?)null : (char)nextValue;

                if (depth == 0)
                {
                    outsideBuffer.Append(currentChar);

                    if (currentChar == '"' && !isEscaped)
                    {
                        if (inQuotedString)
                            inQuotedString = false;
                        else if (CanStartQuotedValue(hasPreviousChar, previousChar))
                            inQuotedString = true;
                    }

                    if (!inQuotedString && currentChar == '(' && IsStructuralOpenParen(hasPreviousChar, previousChar, nextChar))
                    {
                        string tag = ExtractTag(outsideBuffer.ToString(0, Math.Max(0, outsideBuffer.Length - 1)));
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            currentTag = tag;
                            depth = 1;
                            contentBuffer.Clear();
                            outsideBuffer.Clear();
                        }
                    }
                }
                else
                {
                    if (currentChar == '"' && !isEscaped)
                    {
                        if (inQuotedString)
                            inQuotedString = false;
                        else if (CanStartQuotedValue(hasPreviousChar, previousChar))
                            inQuotedString = true;
                    }

                    if (!inQuotedString)
                    {
                        if (currentChar == '(' && IsStructuralOpenParen(hasPreviousChar, previousChar, nextChar))
                        {
                            if (ShouldTreatAsLiteralNameValueOpen(contentBuffer) || IsInsideUnquotedNameValueOnCurrentLine(contentBuffer))
                                contentBuffer.Append(currentChar);
                            else
                            {
                                depth++;
                                contentBuffer.Append(currentChar);
                            }
                        }
                        else if (currentChar == ')' && IsStructuralCloseParen(hasPreviousChar, previousChar, nextChar))
                        {
                            if (IsInsideUnquotedNameValueOnCurrentLine(contentBuffer))
                            {
                                contentBuffer.Append(currentChar);
                                continue;
                            }

                            depth--;

                            if (depth == 0)
                            {
                                if (!string.IsNullOrWhiteSpace(currentTag) && entryTagSet.Contains(currentTag))
                                    yield return (currentTag!, contentBuffer.ToString());

                                currentTag = null;
                                contentBuffer.Clear();
                                continue;
                            }

                            contentBuffer.Append(currentChar);
                        }
                        else
                        {
                            contentBuffer.Append(currentChar);
                        }
                    }
                    else
                    {
                        contentBuffer.Append(currentChar);
                    }
                }

                isEscaped = currentChar == '\\' && !isEscaped;
                if (currentChar != '\\' && isEscaped)
                    isEscaped = false;
                previousChar = currentChar;
                hasPreviousChar = true;
            }
        }

        private static List<TopLevelBlock> ReadTopLevelBlocks(string datFile)
        {
            List<TopLevelBlock> blocks = new List<TopLevelBlock>();

            using (StreamReader reader = new StreamReader(datFile))
            {
                StringBuilder outsideBuffer = new StringBuilder();
                StringBuilder contentBuffer = new StringBuilder();

                bool inQuotedString = false;
                bool isEscaped = false;
                bool hasPreviousChar = false;
                char previousChar = '\0';
                int depth = 0;
                string? currentTag = null;

                int value;
                while ((value = reader.Read()) != -1)
                {
                    char currentChar = (char)value;
                    int nextValue = reader.Peek();
                    char? nextChar = nextValue == -1 ? (char?)null : (char)nextValue;

                    if (depth == 0)
                    {
                        outsideBuffer.Append(currentChar);

                        if (currentChar == '"' && !isEscaped)
                        {
                            if (inQuotedString)
                            {
                                inQuotedString = false;
                            }
                            else if (CanStartQuotedValue(hasPreviousChar, previousChar))
                            {
                                inQuotedString = true;
                            }
                        }

                        if (!inQuotedString && currentChar == '(' && IsStructuralOpenParen(hasPreviousChar, previousChar, nextChar))
                        {
                            string tag = ExtractTag(outsideBuffer.ToString(0, Math.Max(0, outsideBuffer.Length - 1)));

                            if (!string.IsNullOrWhiteSpace(tag))
                            {
                                currentTag = tag;
                                depth = 1;
                                contentBuffer.Clear();
                                outsideBuffer.Clear();
                            }
                        }
                    }
                    else
                    {
                        if (currentChar == '"' && !isEscaped)
                        {
                            if (inQuotedString)
                            {
                                inQuotedString = false;
                            }
                            else if (CanStartQuotedValue(hasPreviousChar, previousChar))
                            {
                                inQuotedString = true;
                            }
                        }

                        if (!inQuotedString)
                        {
                            if (currentChar == '(' && IsStructuralOpenParen(hasPreviousChar, previousChar, nextChar))
                            {
                                if (ShouldTreatAsLiteralNameValueOpen(contentBuffer) || IsInsideUnquotedNameValueOnCurrentLine(contentBuffer))
                                {
                                    contentBuffer.Append(currentChar);
                                }
                                else
                                {
                                    depth++;
                                    contentBuffer.Append(currentChar);
                                }
                            }
                            else if (currentChar == ')' && IsStructuralCloseParen(hasPreviousChar, previousChar, nextChar))
                            {
                                if (IsInsideUnquotedNameValueOnCurrentLine(contentBuffer))
                                {
                                    contentBuffer.Append(currentChar);
                                    continue;
                                }

                                depth--;

                                if (depth == 0)
                                {
                                    if (!string.IsNullOrWhiteSpace(currentTag))
                                    {
                                        blocks.Add(new TopLevelBlock
                                        {
                                            Tag = currentTag,
                                            Content = contentBuffer.ToString()
                                        });
                                    }

                                    currentTag = null;
                                    contentBuffer.Clear();
                                    continue;
                                }

                                contentBuffer.Append(currentChar);
                            }
                            else
                            {
                                contentBuffer.Append(currentChar);
                            }
                        }
                        else
                        {
                            contentBuffer.Append(currentChar);
                        }
                    }

                    isEscaped = currentChar == '\\' && !isEscaped;
                    if (currentChar != '\\' && isEscaped)
                    {
                        isEscaped = false;
                    }

                    previousChar = currentChar;
                    hasPreviousChar = true;
                }
            }

            return blocks;
        }

        private static bool ShouldTreatAsLiteralNameValueOpen(StringBuilder contentBuffer)
        {
            if (!TryGetPreviousWord(contentBuffer, out string previousWord, out bool previousWordAtLineStart))
            {
                return false;
            }

            return !previousWordAtLineStart && string.Equals(previousWord, "name", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInsideUnquotedNameValueOnCurrentLine(StringBuilder contentBuffer)
        {
            if (contentBuffer.Length == 0)
            {
                return false;
            }

            int lineStart = contentBuffer.Length - 1;
            while (lineStart >= 0 && contentBuffer[lineStart] != '\n' && contentBuffer[lineStart] != '\r')
            {
                lineStart--;
            }
            lineStart++;

            int nameTokenIndex = IndexOfIgnoreCase(contentBuffer, lineStart, contentBuffer.Length, " name ");
            if (nameTokenIndex < 0)
            {
                return false;
            }

            int valueStart = nameTokenIndex + 6;
            while (valueStart < contentBuffer.Length && char.IsWhiteSpace(contentBuffer[valueStart]))
            {
                valueStart++;
            }

            if (valueStart >= contentBuffer.Length || contentBuffer[valueStart] == '"')
            {
                return false;
            }

            string[] followingKeys = new[] { " size ", " date ", " crc ", " md5 ", " sha1 ", " sha256 ", " status " };
            foreach (string key in followingKeys)
            {
                if (IndexOfIgnoreCase(contentBuffer, valueStart, contentBuffer.Length, key) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int IndexOfIgnoreCase(StringBuilder buffer, int startInclusive, int endExclusive, string value)
        {
            if (string.IsNullOrEmpty(value) || startInclusive < 0 || endExclusive > buffer.Length || startInclusive >= endExclusive)
            {
                return -1;
            }

            int lastStart = endExclusive - value.Length;
            for (int index = startInclusive; index <= lastStart; index++)
            {
                bool match = true;
                for (int offset = 0; offset < value.Length; offset++)
                {
                    char a = char.ToUpperInvariant(buffer[index + offset]);
                    char b = char.ToUpperInvariant(value[offset]);
                    if (a != b)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool TryGetPreviousWord(StringBuilder contentBuffer, out string word, out bool atLineStart)
        {
            word = string.Empty;
            atLineStart = false;

            if (contentBuffer.Length == 0)
            {
                return false;
            }

            int index = contentBuffer.Length - 1;
            while (index >= 0 && (contentBuffer[index] == ' ' || contentBuffer[index] == '\t'))
            {
                index--;
            }

            if (index < 0)
            {
                return false;
            }

            int end = index;
            while (index >= 0 && IsTagCharacter(contentBuffer[index]))
            {
                index--;
            }

            if (end == index)
            {
                return false;
            }

            word = contentBuffer.ToString(index + 1, end - index);

            while (index >= 0 && (contentBuffer[index] == ' ' || contentBuffer[index] == '\t'))
            {
                index--;
            }

            atLineStart = index < 0 || contentBuffer[index] == '\n' || contentBuffer[index] == '\r';
            return true;
        }

        private static string ExtractTag(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int index = text.Length - 1;
            while (index >= 0 && char.IsWhiteSpace(text[index]))
            {
                index--;
            }

            if (index < 0)
            {
                return string.Empty;
            }

            int end = index;
            while (index >= 0 && IsTagCharacter(text[index]))
            {
                index--;
            }

            return text.Substring(index + 1, end - index).Trim();
        }

        private static bool IsTagCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '-' || value == '.';
        }

        private static List<Token> Tokenize(string text)
        {
            List<Token> tokens = new List<Token>();
            StringBuilder tokenBuilder = new StringBuilder();

            bool inQuotedString = false;
            bool isEscaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char currentChar = text[i];
                char? previousChar = i > 0 ? text[i - 1] : (char?)null;
                char? nextChar = i < text.Length - 1 ? text[i + 1] : (char?)null;

                if (inQuotedString)
                {
                    if (isEscaped)
                    {
                        tokenBuilder.Append(currentChar);
                        isEscaped = false;
                        continue;
                    }

                    if (currentChar == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (currentChar == '"')
                    {
                        tokens.Add(new Token { Type = TokenType.Quoted, Value = tokenBuilder.ToString() });
                        tokenBuilder.Clear();
                        inQuotedString = false;
                        continue;
                    }

                    tokenBuilder.Append(currentChar);
                    continue;
                }

                if (char.IsWhiteSpace(currentChar))
                {
                    FlushWordToken(tokens, tokenBuilder);
                    continue;
                }

                if (currentChar == '"')
                {
                    if (tokenBuilder.Length == 0)
                    {
                        FlushWordToken(tokens, tokenBuilder);
                        inQuotedString = true;
                    }
                    else
                    {
                        tokenBuilder.Append(currentChar);
                    }
                    continue;
                }

                if (currentChar == '(')
                {
                    if (!IsStructuralOpenParen(previousChar.HasValue, previousChar.GetValueOrDefault(), nextChar))
                    {
                        tokenBuilder.Append(currentChar);
                        continue;
                    }

                    if (ShouldTreatAsLiteralNameValueOpen(tokens, tokenBuilder))
                    {
                        tokenBuilder.Append(currentChar);
                        continue;
                    }

                    FlushWordToken(tokens, tokenBuilder);
                    tokens.Add(new Token { Type = TokenType.OpenParen, Value = "(" });
                    continue;
                }

                if (currentChar == ')')
                {
                    if (!IsStructuralCloseParen(previousChar.HasValue, previousChar.GetValueOrDefault(), nextChar))
                    {
                        tokenBuilder.Append(currentChar);
                        continue;
                    }

                    FlushWordToken(tokens, tokenBuilder);
                    tokens.Add(new Token { Type = TokenType.CloseParen, Value = ")" });
                    continue;
                }

                tokenBuilder.Append(currentChar);
            }

            FlushWordToken(tokens, tokenBuilder);
            return tokens;
        }

        private static bool CanStartQuotedValue(bool hasPreviousChar, char previousChar)
        {
            return !hasPreviousChar || char.IsWhiteSpace(previousChar) || previousChar == '(';
        }

        private static bool ShouldTreatAsLiteralNameValueOpen(List<Token> tokens, StringBuilder tokenBuilder)
        {
            if (tokenBuilder.Length > 0 || tokens.Count == 0)
            {
                return false;
            }

            Token previousToken = tokens[tokens.Count - 1];
            return previousToken.Type == TokenType.Word &&
                   string.Equals(previousToken.Value, "name", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStructuralOpenParen(bool hasPreviousChar, char previousChar, char? nextChar)
        {
            bool validPrefix = !hasPreviousChar || char.IsWhiteSpace(previousChar) || previousChar == ')';
            bool validSuffix = !nextChar.HasValue || char.IsWhiteSpace(nextChar.Value) || nextChar.Value == '"';
            return validPrefix && validSuffix;
        }

        private static bool IsStructuralCloseParen(bool hasPreviousChar, char previousChar, char? nextChar)
        {
            bool validPrefix = !hasPreviousChar || char.IsWhiteSpace(previousChar) || previousChar == '"' || previousChar == ')';
            bool validSuffix = !nextChar.HasValue || char.IsWhiteSpace(nextChar.Value) || nextChar.Value == '(';
            return validPrefix && validSuffix;
        }

        private static void FlushWordToken(List<Token> tokens, StringBuilder tokenBuilder)
        {
            if (tokenBuilder.Length == 0)
            {
                return;
            }

            tokens.Add(new Token { Type = TokenType.Word, Value = tokenBuilder.ToString() });
            tokenBuilder.Clear();
        }

        private static Dictionary<string, string> ParseAttributes(List<Token> tokens, int start, int endExclusive)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int index = start;

            while (index < endExclusive)
            {
                Token token = tokens[index];

                if (token.Type != TokenType.Word || !IsKeyCandidate(token.Value))
                {
                    index++;
                    continue;
                }

                string key = token.Value;
                int valueStartIndex = index + 1;

                if (valueStartIndex >= endExclusive)
                {
                    break;
                }

                if (tokens[valueStartIndex].Type == TokenType.OpenParen)
                {
                    int nestedDepth = 1;
                    int nestedIndex = valueStartIndex + 1;

                    while (nestedIndex < endExclusive && nestedDepth > 0)
                    {
                        if (tokens[nestedIndex].Type == TokenType.OpenParen)
                        {
                            nestedDepth++;
                        }
                        else if (tokens[nestedIndex].Type == TokenType.CloseParen)
                        {
                            nestedDepth--;
                        }

                        nestedIndex++;
                    }

                    index = nestedIndex;
                    continue;
                }

                List<string> valueParts = new List<string>();
                int valueIndex = valueStartIndex;

                if (tokens[valueIndex].Type == TokenType.Quoted)
                {
                    valueParts.Add(tokens[valueIndex].Value);
                    valueIndex++;
                }
                else
                {
                    while (valueIndex < endExclusive &&
                           tokens[valueIndex].Type != TokenType.OpenParen &&
                           tokens[valueIndex].Type != TokenType.CloseParen)
                    {
                        if (valueParts.Count > 0 && IsLikelyNextKey(tokens, valueIndex, endExclusive))
                        {
                            break;
                        }

                        valueParts.Add(tokens[valueIndex].Value);
                        valueIndex++;
                    }
                }

                attributes[key] = string.Join(" ", valueParts).Trim();
                index = Math.Max(index + 1, valueIndex);
            }

            return attributes;
        }

        private static List<Dictionary<string, object>> ParseChildrenFromLines(string content, HashSet<string>? childTagSet)
        {
            List<Dictionary<string, object>> children = new List<Dictionary<string, object>>();

            using (StringReader reader = new StringReader(content))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        continue;
                    }

                    int tagEnd = 0;
                    while (tagEnd < trimmed.Length && IsTagCharacter(trimmed[tagEnd]))
                    {
                        tagEnd++;
                    }

                    if (tagEnd == 0)
                    {
                        continue;
                    }

                    string childTag = trimmed.Substring(0, tagEnd);
                    bool includeChild = childTagSet == null || childTagSet.Count == 0 || childTagSet.Contains(childTag);
                    if (!includeChild)
                    {
                        continue;
                    }

                    int openParenIndex = trimmed.IndexOf('(', tagEnd);
                    int closeParenIndex = trimmed.LastIndexOf(')');
                    if (openParenIndex < 0 || closeParenIndex <= openParenIndex)
                    {
                        continue;
                    }

                    string innerContent = trimmed.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1);
                    List<Token> tokens = Tokenize(innerContent);

                    Dictionary<string, object> child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["tag"] = childTag,
                        ["attributes"] = ParseAttributes(tokens, 0, tokens.Count)
                    };

                    children.Add(child);
                }
            }

            return children;
        }

        private static bool IsLikelyNextKey(List<Token> tokens, int index, int endExclusive)
        {
            if (index >= endExclusive)
            {
                return false;
            }

            Token token = tokens[index];
            if (token.Type != TokenType.Word || !IsKeyCandidate(token.Value))
            {
                return false;
            }

            if (index + 1 >= endExclusive)
            {
                return false;
            }

            Token nextToken = tokens[index + 1];
            return nextToken.Type != TokenType.CloseParen;
        }

        private static bool IsKeyCandidate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!char.IsLetter(value[0]) && value[0] != '_')
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char current = value[index];
                if (!char.IsLetterOrDigit(current) && current != '_' && current != '-')
                {
                    return false;
                }
            }

            return true;
        }

    }
}