using System;
using System.Globalization;

namespace Avalonia.Ide.CompletionEngine;

internal enum SelectorStatement
{
    Start,
    Middle,
    Colon,
    Class,
    Name,
    CanHaveType,
    Traversal,
    TypeName,
    Property,
    AttachedProperty,
    Template,
    Value,
    Function,
    FunctionArgs,
    End,
}

internal ref struct SelectorParser
{
    private ref struct ParserContext
    {
        private ReadOnlySpan<char> _data;
        private ReadOnlySpan<char> _original;
        public ParserContext(ReadOnlySpan<char> data) :
            this()
        {
            _data = data;
            _original = data;
        }
        private SelectorStatement statement = SelectorStatement.Start;
        public int NamespaceStart = -1;
        public int NamespaceEnd = -1;
        public int TypeNameStart = -1;
        public int TypeNameEnd = -1;
        public int ClassNameStart = -1;
        public int ClassNameEnd = -1;
        public int PropertyNameStart = -1;
        public int PropertyNameEnd = -1;
        public int ValueStart = -1;
        public int ValueEnd = -1;
        public int NameStart = -1;
        public int NameEnd = -1;
        public int FunctionNameStart = -1;
        public int FunctionNameEnd = -1;
        public int Position { get; private set; }
        public bool IsError = false;
        public char Peek => _data[0];
        public bool End =>
            _data.IsEmpty;
        public int? LastParsedPosition = default;
        public bool IsTemplate;
        public int TemplateOwnerStart = -1;
        public int TemplateOwnerEnd = -1;
        public int NamespaceTemplateOwnerStart = -1;
        public int NamespaceTemplateOwnerEnd = -1;
        public int LastSegmentStartPosition;

        public SelectorStatement PreviousStatement { get; private set; }

        public SelectorStatement Statement
        {
            get => statement;
            set
            {
                if (statement != value)
                {
                    if (value is SelectorStatement.Start or SelectorStatement.Middle)
                    {
                        LastSegmentStartPosition = Position;
                    }
                    if (value is SelectorStatement.Start)
                    {
                        (NamespaceStart, NamespaceEnd, TypeNameStart, TypeNameEnd, ClassNameStart, ClassNameEnd, PropertyNameStart, PropertyNameEnd, NameStart, NameEnd, ValueStart, ValueEnd, FunctionNameStart, FunctionNameEnd) =
                            (-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
                    }
                    PreviousStatement = statement;
                }
                statement = value;
            }
        }

        public char Take()
        {
            Position++;
            var take = _data[0];
            _data = _data.Slice(1);
            return take;
        }

        public void SkipWhitespace()
        {
            var trimmed = _data.TrimStart();
            Position += _data.Length - trimmed.Length;
            _data = trimmed;
        }

        public bool TakeIf(char c)
        {
            if (!End && Peek == c)
            {
                Take();
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool TakeIf(Func<char, bool> condition)
        {
            if (condition(Peek))
            {
                Take();
                return true;
            }
            return false;
        }

        public ReadOnlySpan<char> TakeUntil(char c)
        {
            int len;
            for (len = 0; len < _data.Length && _data[len] != c; len++)
            {
            }
            var span = _data.Slice(0, len);
            _data = _data.Slice(len);
            Position += len;
            return span;
        }

        public ReadOnlySpan<char> TakeWhile(Func<char, bool> condition)
        {
            int len;
            for (len = 0; len < _data.Length && condition(_data[len]); len++)
            {
            }
            var span = _data.Slice(0, len);
            _data = _data.Slice(len);
            Position += len;
            return span;
        }

        public ReadOnlySpan<char> TryPeek(int count)
        {
            if (_data.Length < count)
                return ReadOnlySpan<char>.Empty;
            return _data.Slice(0, count);
        }

        public ReadOnlySpan<char> PeekWhitespace()
        {
            var trimmed = _data.TrimStart();
            return _data.Slice(0, _data.Length - trimmed.Length);
        }

        public void Skip(int count)
        {
            if (_data.Length < count)
                throw new IndexOutOfRangeException();
            _data = _data.Slice(count);
        }

        public ReadOnlySpan<char> ParseStyleClass()
        {
            if (!End && IsValidIdentifierStart(Peek))
            {
                return TakeWhile(c => IsValidIdentifierChar(c));
            }
            return ReadOnlySpan<char>.Empty;
        }

        public ReadOnlySpan<char> ParseIdentifier()
        {
            if (!End && IsValidIdentifierStart(Peek))
            {
                return TakeWhile(c => IsValidIdentifierChar(c));
            }
            return ReadOnlySpan<char>.Empty;
        }

        private static bool IsValidIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        private static bool IsValidIdentifierChar(char c)
        {
            if (IsValidIdentifierStart(c) || c == '-')
            {
                return true;
            }
            else
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                return cat == UnicodeCategory.NonSpacingMark ||
                       cat == UnicodeCategory.SpacingCombiningMark ||
                       cat == UnicodeCategory.ConnectorPunctuation ||
                       cat == UnicodeCategory.Format ||
                       cat == UnicodeCategory.DecimalDigitNumber;
            }
        }

        public ReadOnlySpan<char> GetRange(int from, int to)
        {
            if (from < 0 || from > _original.Length)
            {
                return ReadOnlySpan<char>.Empty;
            }
            if (to < 0 || to > _original.Length)
            {
                return _original.Slice(from);
            }
            else
            {
                return _original.Slice(from, to - from);
            }
        }

    }

    ParserContext _context;

    private SelectorParser(ReadOnlySpan<char> data)
    {
        _context = new ParserContext(data);
    }

    public string? Namespace =>
        _context.GetRange(_context.NamespaceStart, _context.NamespaceEnd).ToString();

    public string? TypeName =>
        _context.GetRange(_context.TypeNameStart, _context.TypeNameEnd).ToString();

    public string? Class =>
        _context.GetRange(_context.ClassNameStart, _context.ClassNameEnd).ToString();

    public string? PropertyName =>
        _context.GetRange(_context.PropertyNameStart, _context.PropertyNameEnd).ToString();

    public string? Value =>
        _context.GetRange(_context.ValueStart, _context.ValueEnd).ToString();

    public string? ElementName =>
        _context.GetRange(_context.NameStart, _context.NameEnd).ToString();

    public string? FunctionName =>
        _context.GetRange(_context.FunctionNameStart, _context.FunctionNameEnd).ToString();

    public SelectorStatement Statement =>
        _context.Statement;

    public bool IsError =>
        _context.IsError;

    public int? LastParsedPosition =>
        _context.LastParsedPosition;

    public SelectorStatement PreviousStatement =>
        _context.PreviousStatement;

    public bool IsTemplate =>
        _context.IsTemplate;

    public int LastSegmentStartPosition =>
        _context.LastSegmentStartPosition;

    public string? TemplateOwner
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            if (_context.NamespaceTemplateOwnerEnd > -1)
            {
#if NET5_0_OR_GREATER
                sb.Append(_context.GetRange(_context.NamespaceTemplateOwnerStart, _context.NamespaceTemplateOwnerEnd));
#else
                sb.Append(_context.GetRange(_context.NamespaceTemplateOwnerStart, _context.NamespaceTemplateOwnerEnd).ToArray());
#endif
                sb.Append(':');
            }
#if NET5_0_OR_GREATER
            sb.Append(_context.GetRange(_context.TemplateOwnerStart, _context.TemplateOwnerEnd));
#else
            sb.Append(_context.GetRange(_context.TemplateOwnerStart, _context.TemplateOwnerEnd).ToArray());
#endif
            return sb.ToString();
        }
    }

    public static SelectorParser Parse(ReadOnlySpan<char> data)
    {
        var selector = new SelectorParser(data);
        selector.Parse();
        return selector;
    }


    private void Parse()
    {
        Parse(ref _context);
    }

    private static void Parse(ref ParserContext context, char? end = default)
    {
        while (!context.End && !context.IsError && context.Statement != SelectorStatement.End)
        {
            switch (context.Statement)
            {
                case SelectorStatement.Start:
                    ParseStart(ref context);
                    break;
                case SelectorStatement.Middle:
                    ParseMiddle(ref context, end);
                    break;
                case SelectorStatement.Colon:
                    ParseColon(ref context);
                    break;
                case SelectorStatement.Class:
                    ParseClass(ref context);
                    break;
                case SelectorStatement.Name:
                    ParseName(ref context);
                    break;
                case SelectorStatement.CanHaveType:
                    ParseCanHaveType(ref context);
                    break;
                case SelectorStatement.Traversal:
                    ParseTraversal(ref context);
                    break;
                case SelectorStatement.TypeName:
                    ParseTypeName(ref context);
                    break;
                case SelectorStatement.Property:
                    ParseProperty(ref context);
                    break;
                case SelectorStatement.AttachedProperty:
                    ParseAttachedProperty(ref context);
                    break;
                case SelectorStatement.Template:
                    ParseTemplate(ref context);
                    break;
                case SelectorStatement.FunctionArgs:
                    ParseFunctionArgs(ref context);
                    break;
                case SelectorStatement.End:
                    break;
                default:
                    break;
            }
        }
    }

    private static void ParseFunctionArgs(ref ParserContext context)
    {
        context.Statement = SelectorStatement.Middle;
    }

    private static void ParseStart(ref ParserContext context)
    {
        context.SkipWhitespace();
        if (context.End)
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.End;
        }

        if (context.TakeIf(':'))
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Colon;
        }
        else if (context.TakeIf('.'))
        {

            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Class;
        }
        else if (context.TakeIf('#'))
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Name;
        }
        else if (context.TakeIf('^'))
        {

            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.CanHaveType;
        }
        else if (!context.End)
        {
            context.Statement = SelectorStatement.Middle;
        }
    }

    private static void ParseMiddle(ref ParserContext context, char? end)
    {
        if (context.TakeIf(':'))
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Colon;
        }
        else if (context.TakeIf('.'))
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Class;
        }
        else if (context.TakeIf(char.IsWhiteSpace) || context.Peek == '>')
        {

            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Traversal;
        }
        else if (context.TakeIf('/'))
        {

            context.Statement = SelectorStatement.Template;
        }
        else if (context.TakeIf('#'))
        {

            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Name;
        }
        else if (context.TakeIf(','))
        {

            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.Start;
        }
        else if (context.TakeIf('^'))
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.CanHaveType;
        }
        else if (end.HasValue && !context.End && context.Peek == end.Value)
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.End;
        }
        else
        {
            context.LastParsedPosition = context.Position;
            context.Statement = SelectorStatement.TypeName;
        }
    }

    private static void ParseColon(ref ParserContext r)
    {
        var start = r.Position;
        var identifier = r.ParseStyleClass();

        if (identifier.IsEmpty)
        {
            r.IsError = true;
            return;
        }

        const string IsKeyword = "is";
        const string NotKeyword = "not";
        const string NthChildKeyword = "nth-child";
        const string NthLastChildKeyword = "nth-last-child";

        if (identifier.SequenceEqual(IsKeyword.AsSpan()))
        {
            r.FunctionNameStart = start;
            r.Statement = SelectorStatement.Function;
            r.LastParsedPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.Statement = SelectorStatement.FunctionArgs;
                r.FunctionNameEnd = r.Position - 1;
                if (r.End)
                {
                    return;
                }
                r.Statement = SelectorStatement.TypeName;
                ParseType(ref r);
                if (!Expect(ref r, ')'))
                {
                    return;
                }
                r.Statement = SelectorStatement.Middle;
            }
        }
        else if (identifier.SequenceEqual(NotKeyword.AsSpan()))
        {
            r.FunctionNameStart = start;
            r.Statement = SelectorStatement.Function;
            r.LastParsedPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.FunctionNameEnd = r.Position - 1;
                r.Statement = SelectorStatement.FunctionArgs;
                Parse(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.Statement = SelectorStatement.FunctionArgs;
                Expect(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.Statement = SelectorStatement.Middle;
            }
        }
        else if (identifier.SequenceEqual(NthChildKeyword.AsSpan()))
        {
            r.FunctionNameStart = start;
            r.Statement = SelectorStatement.Function;
            r.LastParsedPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.FunctionNameEnd = r.Position - 1;
                r.Statement = SelectorStatement.FunctionArgs;
                r.TakeUntil(')');
                Expect(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.Statement = SelectorStatement.Middle;
                r.LastParsedPosition = r.Position;
                return;
            }

        }
        else if (identifier.SequenceEqual(NthLastChildKeyword.AsSpan()))
        {
            r.FunctionNameStart = start;
            r.Statement = SelectorStatement.Function;
            r.LastParsedPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.FunctionNameEnd = r.Position - 1;
                r.Statement = SelectorStatement.FunctionArgs;
                r.TakeUntil(')');
                Expect(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.LastParsedPosition = r.Position;
                r.Statement = SelectorStatement.Middle;
            }
        }
        else
        {
            r.ClassNameStart = start;
            r.ClassNameEnd = r.Position;
            r.LastParsedPosition = r.Position;
            r.Statement = SelectorStatement.CanHaveType;
        }
    }

    private static void ParseClass(ref ParserContext r)
    {
        r.ClassNameStart = r.Position;
        var @class = r.ParseStyleClass();
        if (@class.IsEmpty)
        {
            r.IsError = true;
            return;
        }
        r.ClassNameEnd = r.Position;
        r.LastParsedPosition = r.Position;
        r.Statement = SelectorStatement.CanHaveType;
    }

    private static void ParseName(ref ParserContext r)
    {
        r.NameStart = r.Position;
        var name = r.ParseIdentifier();
        if (name.IsEmpty)
        {
            r.IsError = true;
            return;
        }
        r.NameEnd = r.Position;
        if (!r.End)
            r.Statement = SelectorStatement.CanHaveType;
    }

    private static void ParseCanHaveType(ref ParserContext r)
    {
        if (r.TakeIf('['))
        {
            r.LastParsedPosition = r.Position;
            r.Statement = SelectorStatement.Property;
        }
        else
        {
            r.Statement = SelectorStatement.Middle;
        }
    }

    private static void ParseTraversal(ref ParserContext r)
    {
        r.SkipWhitespace();
        if (r.TakeIf('>'))
        {
            r.SkipWhitespace();
            r.Statement = SelectorStatement.Middle;
        }
        else if (r.TakeIf('/'))
        {
            r.LastParsedPosition = r.Position;
            r.Statement = SelectorStatement.Template;
        }
        else if (!r.End)
        {
            r.Statement = SelectorStatement.Middle;
        }
        else
        {
            r.LastParsedPosition = r.Position;
            r.Statement = SelectorStatement.End;
        }
    }

    private static void ParseTypeName(ref ParserContext r)
    {
        ParseType(ref r);
        if (r.IsError)
        {
            return;
        }
        r.LastParsedPosition = r.Position;
        r.Statement = SelectorStatement.CanHaveType;
    }

    private static void ParseProperty(ref ParserContext r)
    {
        r.LastParsedPosition = r.Position;
        r.PropertyNameStart = r.Position;
        var property = r.ParseIdentifier();

        if (r.End)
        {
            r.IsError = true;
            return;
        }

        if (r.TakeIf('('))
        {
            r.Statement = SelectorStatement.AttachedProperty;
            return;
        }
        else if (!r.TakeIf('='))
        {
            r.IsError = true;
        }
        r.PropertyNameEnd = r.Position - 1;
        r.LastParsedPosition = r.Position;
        r.Statement = SelectorStatement.Value;
        r.ValueStart = r.Position;
        _ = r.TakeUntil(']');
        if (!Expect(ref r, ']'))
        {
            return;
        }
        r.ValueEnd = r.Position;
        r.Statement = SelectorStatement.Property;
        r.LastParsedPosition = r.Position;
        if (!r.End)
        {
            r.Statement = SelectorStatement.Middle;
        }
    }

    private static void ParseAttachedProperty(ref ParserContext r)
    {
        r.LastParsedPosition = r.Position;
        ParseType(ref r);
        if (r.IsError)
        {
            return;
        }
        r.LastParsedPosition = r.Position;
        if (r.End || !r.TakeIf('.'))
        {
            r.IsError = true;
            return;
        }
        r.PropertyNameStart = r.Position;
        if (r.End)
        {
            r.IsError = true;
            return;
        }
        var property = r.ParseIdentifier();
        if (r.End || property.IsEmpty)
        {
            r.IsError = true;
            return;
        }
        r.PropertyNameEnd = r.Position;

        if (!r.TakeIf(')'))
        {
            r.IsError = true;
            return;
        }
        r.SkipWhitespace();
        r.LastParsedPosition = r.Position;

        if (r.End || !r.TakeIf('='))
        {
            r.IsError = true;
            return;
        }
        r.ValueStart = r.Position;
        r.Statement = SelectorStatement.Value;
        _ = r.TakeUntil(']');
        r.ValueEnd = r.Position;
        if (Expect(ref r, ']'))
        {
            r.IsError = true;
            return;
        }
        r.LastParsedPosition = r.Position;
        if (!r.End)
        {
            r.Statement = SelectorStatement.Middle;
        }
    }

    private static void ParseTemplate(ref ParserContext r)
    {
        var template = r.ParseIdentifier();
        const string TemplateKeyword = "template";
        if (!template.SequenceEqual(TemplateKeyword.AsSpan()))
        {
            r.LastParsedPosition = r.Position;
            r.IsError = true;
            return;
        }
        else if (!r.TakeIf('/'))
        {
            r.LastParsedPosition = r.Position;
            r.IsError = true;
            return;
        }
        r.LastParsedPosition = r.Position;
        r.IsTemplate = true;
        (r.TemplateOwnerStart, r.TemplateOwnerEnd, r.NamespaceTemplateOwnerStart, r.NamespaceTemplateOwnerEnd) =
            (r.TypeNameStart, r.TypeNameEnd, r.NamespaceStart, r.NamespaceEnd);
        r.Statement = SelectorStatement.Start;
    }

    private static void ParseType(ref ParserContext r)
    {
        r.LastParsedPosition = r.Position;
        ReadOnlySpan<char> ns = default;
        var startPosition = r.Position;
        var namespaceOrTypeName = r.ParseIdentifier();

        if (namespaceOrTypeName.IsEmpty)
        {
            r.IsError = true;
            return;
        }

        if (!r.End && r.TakeIf('|'))
        {
            ns = namespaceOrTypeName;
            r.NamespaceStart = startPosition;
            r.NamespaceEnd = r.Position - 1;
            if (r.End)
            {
                r.IsError = true;
                return;
            }
            r.TypeNameStart = r.Position;
            _ = r.ParseIdentifier();
            r.TypeNameEnd = r.Position;
        }
        else
        {
            r.TypeNameStart = startPosition;
            r.TypeNameEnd = r.Position;
        }
        r.LastParsedPosition = r.Position;
    }

    private static bool Expect(ref ParserContext r, char c)
    {
        if (r.End || !r.TakeIf(c))
        {
            r.IsError = true;
            return false;
        }
        return true;
    }
}
