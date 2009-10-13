using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ParseCs
{
    public enum TokenType
    {
        Builtin,
        Identifier,
        StringLiteral,
        CharacterLiteral,
        NumberLiteral,
        CommentSlashSlash,
        CommentSlashStar,
        PreprocessorDirective,
    }

    public class Token
    {
        private string _str;
        private TokenType _type;
        private int _index;
        public Token(string str, TokenType type, int index) { _str = str; _type = type; _index = index; }
        public string TokenStr { get { return _str; } }
        public TokenType Type { get { return _type; } }
        public int Index { get { return _index; } }

        public bool IsBuiltin(string name) { return _type == TokenType.Builtin && _str.Equals(name); }
        public bool IsIdentifier(string name) { return _type == TokenType.Identifier && _str.Equals(name); }

        public string Identifier()
        {
            return Identifier(@"Identifier expected.");
        }

        public string Identifier(string errorMessage)
        {
            if (_type != TokenType.Identifier)
                throw new ParseException(errorMessage, _index);
            return _str;
        }

        public void Assert(string token)
        {
            if (!_str.Equals(token))
                throw new ParseException(@"Assertion failed.", _index);
        }

        public override string ToString()
        {
            return _type.ToString() + (_str != null ? ": " + _str : "");
        }
    }
}
