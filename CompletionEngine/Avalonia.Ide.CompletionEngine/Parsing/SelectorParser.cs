using System;
using System.Globalization;

namespace Avalonia.Ide.CompletionEngine;



internal enum SelectorStatment
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
        private SelectorStatment statment = SelectorStatment.Start;
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
        public int FuntcionNameStart = -1;
        public int FunctionNameEnd = -1;
        public int Position { get; private set; }
        public bool IsError = false;
        public char Peek => _data[0];
        public bool End =>
            _data.IsEmpty;
        public int? LastParseredPosition = default;
        public bool IsTemplate;
        public int TemplateOnwnerStart = -1;
        public int TemplateOnwnerEnd = -1;
        public int NamespaceTemplateOnwnerStart = -1;
        public int NamespaceTemplateOnwnerEnd = -1;
        public int LastSegmentStartPosition;

        public SelectorStatment PreviusStatment { get; private set; }

        public SelectorStatment Statment
        {
            get => statment;
            set
            {
                if (statment != value)
                {
                    if (value is SelectorStatment.Start or SelectorStatment.Middle)
                    {
                        LastSegmentStartPosition = Position;
                    }
                    if (value is SelectorStatment.Start)
                    {
                        (NamespaceStart, NamespaceEnd, TypeNameStart, TypeNameEnd, ClassNameStart, ClassNameEnd, PropertyNameStart, PropertyNameEnd, NameStart, NameEnd, ValueStart, ValueEnd, FuntcionNameStart, FunctionNameEnd) =
                            (-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
                    }
                    PreviusStatment = statment;
                }
                statment = value;
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
        _context.GetRange(_context.FuntcionNameStart, _context.FunctionNameEnd).ToString();

    public SelectorStatment Statment =>
        _context.Statment;

    public bool IsError =>
        _context.IsError;

    public int? LastParseredPosition =>
        _context.LastParseredPosition;

    public SelectorStatment PreviusStatment =>
        _context.PreviusStatment;

    public bool IsTemplate =>
        _context.IsTemplate;

    public int LastSegmentStartPosition =>
        _context.LastSegmentStartPosition;

    public string? TemplateOwner
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            if (_context.NamespaceTemplateOnwnerEnd > -1)
            {
#if NET5_0_OR_GREATER
                sb.Append(_context.GetRange(_context.NamespaceTemplateOnwnerStart, _context.NamespaceTemplateOnwnerEnd));
#else
                sb.Append(_context.GetRange(_context.NamespaceTemplateOnwnerStart, _context.NamespaceTemplateOnwnerEnd).ToArray());
#endif
                sb.Append(':');
            }
#if NET5_0_OR_GREATER
            sb.Append(_context.GetRange(_context.TemplateOnwnerStart, _context.TemplateOnwnerEnd));
#else
            sb.Append(_context.GetRange(_context.TemplateOnwnerStart, _context.TemplateOnwnerEnd).ToArray());
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
        while (!context.End && !context.IsError && context.Statment != SelectorStatment.End)
        {
            switch (context.Statment)
            {
                case SelectorStatment.Start:
                    ParseStart(ref context);
                    break;
                case SelectorStatment.Middle:
                    ParseMiddle(ref context, end);
                    break;
                case SelectorStatment.Colon:
                    ParseColon(ref context);
                    break;
                case SelectorStatment.Class:
                    ParseClass(ref context);
                    break;
                case SelectorStatment.Name:
                    ParseName(ref context);
                    break;
                case SelectorStatment.CanHaveType:
                    ParseCanHaveType(ref context);
                    break;
                case SelectorStatment.Traversal:
                    ParseTraversal(ref context);
                    break;
                case SelectorStatment.TypeName:
                    ParseTypeName(ref context);
                    break;
                case SelectorStatment.Property:
                    ParseProperty(ref context);
                    break;
                case SelectorStatment.AttachedProperty:
                    ParseAttachedProperty(ref context);
                    break;
                case SelectorStatment.Template:
                    ParseTemplate(ref context);
                    break;
                case SelectorStatment.FunctionArgs:
                    ParseFunctionArgs(ref context);
                    break;
                case SelectorStatment.End:
                    break;
                default:
                    break;
            }
        }
    }

    private static void ParseFunctionArgs(ref ParserContext context)
    {
        context.Statment = SelectorStatment.Middle;
    }

    private static void ParseStart(ref ParserContext context)
    {
        context.SkipWhitespace();
        if (context.End)
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.End;
        }

        if (context.TakeIf(':'))
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Colon;
        }
        else if (context.TakeIf('.'))
        {

            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Class;
        }
        else if (context.TakeIf('#'))
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Name;
        }
        else if (context.TakeIf('^'))
        {

            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.CanHaveType;
        }
        else if (!context.End)
        {
            context.Statment = SelectorStatment.Middle;
        }
    }

    private static void ParseMiddle(ref ParserContext context, char? end)
    {
        if (context.TakeIf(':'))
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Colon;
        }
        else if (context.TakeIf('.'))
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Class;
        }
        else if (context.TakeIf(char.IsWhiteSpace) || context.Peek == '>')
        {

            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Traversal;
        }
        else if (context.TakeIf('/'))
        {

            context.Statment = SelectorStatment.Template;
        }
        else if (context.TakeIf('#'))
        {

            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Name;
        }
        else if (context.TakeIf(','))
        {

            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.Start;
        }
        else if (context.TakeIf('^'))
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.CanHaveType;
        }
        else if (end.HasValue && !context.End && context.Peek == end.Value)
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.End;
        }
        else
        {
            context.LastParseredPosition = context.Position;
            context.Statment = SelectorStatment.TypeName;
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
            r.FuntcionNameStart = start;
            r.Statment = SelectorStatment.Function;
            r.LastParseredPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.Statment = SelectorStatment.FunctionArgs;
                r.FunctionNameEnd = r.Position - 1;
                if (r.End)
                {
                    return;
                }
                r.Statment = SelectorStatment.TypeName;
                ParseType(ref r);
                if (!Expect(ref r, ')'))
                {
                    return;
                }
                r.Statment = SelectorStatment.Middle;
            }
        }
        else if (identifier.SequenceEqual(NotKeyword.AsSpan()))
        {
            r.FuntcionNameStart = start;
            r.Statment = SelectorStatment.Function;
            r.LastParseredPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.FunctionNameEnd = r.Position - 1;
                r.Statment = SelectorStatment.FunctionArgs;
                Parse(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.Statment = SelectorStatment.FunctionArgs;
                Expect(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.Statment = SelectorStatment.Middle;
            }
        }
        else if (identifier.SequenceEqual(NthChildKeyword.AsSpan()))
        {
            r.FuntcionNameStart = start;
            r.Statment = SelectorStatment.Function;
            r.LastParseredPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.FunctionNameEnd = r.Position - 1;
                r.Statment = SelectorStatment.FunctionArgs;
                //var (step, offset) = ParseNthChildArguments(ref r);
                r.TakeUntil(')');
                Expect(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.Statment = SelectorStatment.Middle;
                r.LastParseredPosition = r.Position;
                return;
            }

        }
        else if (identifier.SequenceEqual(NthLastChildKeyword.AsSpan()))
        {
            r.FuntcionNameStart = start;
            r.Statment = SelectorStatment.Function;
            r.LastParseredPosition = r.Position;
            if (r.TakeIf('('))
            {
                r.FunctionNameEnd = r.Position - 1;
                r.Statment = SelectorStatment.FunctionArgs;
                //var (step, offset) = ParseNthChildArguments(ref r);

                //var syntax = new NthLastChildSyntax { Step = step, Offset = offset };
                r.TakeUntil(')');
                Expect(ref r, ')');
                if (r.IsError)
                {
                    return;
                }
                r.LastParseredPosition = r.Position;
                r.Statment = SelectorStatment.Middle;
            }
        }
        else
        {
            r.ClassNameStart = start;
            r.ClassNameEnd = r.Position;
            r.LastParseredPosition = r.Position;
            r.Statment = SelectorStatment.CanHaveType;
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
        r.LastParseredPosition = r.Position;
        r.Statment = SelectorStatment.CanHaveType;
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
            r.Statment = SelectorStatment.CanHaveType;
    }

    private static void ParseCanHaveType(ref ParserContext r)
    {
        if (r.TakeIf('['))
        {
            r.LastParseredPosition = r.Position;
            r.Statment = SelectorStatment.Property;
        }
        else
        {
            r.Statment = SelectorStatment.Middle;
        }
    }

    private static void ParseTraversal(ref ParserContext r)
    {
        r.SkipWhitespace();
        if (r.TakeIf('>'))
        {
            r.SkipWhitespace();
            //return (State.Middle, new ChildSyntax());
            r.Statment = SelectorStatment.Middle;
        }
        else if (r.TakeIf('/'))
        {
            //return (State.Template, null);
            r.LastParseredPosition = r.Position;
            r.Statment = SelectorStatment.Template;
        }
        else if (!r.End)
        {
            //return (State.Middle, new DescendantSyntax());
            r.Statment = SelectorStatment.Middle;
        }
        else
        {
            //return (State.End, null);
            r.LastParseredPosition = r.Position;
            r.Statment = SelectorStatment.End;
        }
    }

    private static void ParseTypeName(ref ParserContext r)
    {
        ParseType(ref r);
        if (r.IsError)
        {
            return;
        }
        r.LastParseredPosition = r.Position;
        //return (State.CanHaveType, ParseType(ref r, new OfTypeSyntax()));
        r.Statment = SelectorStatment.CanHaveType;
    }

    private static void ParseProperty(ref ParserContext r)
    {
        r.LastParseredPosition = r.Position;
        r.PropertyNameStart = r.Position;
        var property = r.ParseIdentifier();

        if (r.End)
        {
            r.IsError = true;
            return;
        }

        if (r.TakeIf('('))
        {
            //return (State.AttachedProperty, default);
            r.Statment = SelectorStatment.AttachedProperty;
            return;
        }
        else if (!r.TakeIf('='))
        {
            r.IsError = true;
        }
        r.PropertyNameEnd = r.Position - 1;
        r.LastParseredPosition = r.Position;
        r.Statment = SelectorStatment.Value;
        r.ValueStart = r.Position;
        _ = r.TakeUntil(']');
        if (!Expect(ref r, ']'))
        {
            return;
        }
        r.ValueEnd = r.Position;
        r.Statment = SelectorStatment.Property;
        r.LastParseredPosition = r.Position;
        if (!r.End)
        {
            r.Statment = SelectorStatment.Middle;
        }
        //return (State.CanHaveType, new PropertySyntax { Property = property.ToString(), Value = value.ToString() });
    }

    private static void ParseAttachedProperty(ref ParserContext r)
    {
        r.LastParseredPosition = r.Position;
        ParseType(ref r);
        if (r.IsError)
        {
            return;
        }
        r.LastParseredPosition = r.Position;
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
        r.LastParseredPosition = r.Position;

        if (r.End || !r.TakeIf('='))
        {
            r.IsError = true;
            return;
        }
        r.ValueStart = r.Position;
        r.Statment = SelectorStatment.Value;
        _ = r.TakeUntil(']');
        r.ValueEnd = r.Position;
        if (Expect(ref r, ']'))
        {
            r.IsError = true;
            return;
        }
        r.LastParseredPosition = r.Position;
        if (!r.End)
        {
            r.Statment = SelectorStatment.Middle;
        }
    }

    private static void ParseTemplate(ref ParserContext r)
    {
        var template = r.ParseIdentifier();
        const string TemplateKeyword = "template";
        if (!template.SequenceEqual(TemplateKeyword.AsSpan()))
        {
            r.LastParseredPosition = r.Position;
            r.IsError = true;
            return;
        }
        else if (!r.TakeIf('/'))
        {
            r.LastParseredPosition = r.Position;
            r.IsError = true;
            return;
        }
        r.LastParseredPosition = r.Position;
        r.IsTemplate = true;
        (r.TemplateOnwnerStart, r.TemplateOnwnerEnd, r.NamespaceTemplateOnwnerStart, r.NamespaceTemplateOnwnerEnd) =
            (r.TypeNameStart, r.TypeNameEnd, r.NamespaceStart, r.NamespaceEnd);
        r.Statment = SelectorStatment.Start;
    }

    private static void ParseType(ref ParserContext r)
    {
        r.LastParseredPosition = r.Position;
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
        r.LastParseredPosition = r.Position;
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
