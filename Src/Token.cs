﻿using System.Collections.Generic;

namespace RT.ParseCs
{
    enum TokenType
    {
        Builtin,
        Identifier,
        StringLiteral,
        CharacterLiteral,
        NumberLiteral,
        CommentSlashSlash,
        CommentSlashStar,
        PreprocessorDirective,
        EndOfFile
    }

    sealed class Token
    {
        private string _str;
        private TokenType _type;
        private int _startIndex;
        private int _endIndex;
        public Token(string str, TokenType type, int startIndex, int endIndex) { _str = str; _type = type; _startIndex = startIndex; _endIndex = endIndex; }
        public string TokenStr { get { return _str; } }
        public TokenType Type { get { return _type; } }
        public int StartIndex { get { return _startIndex; } }
        public int EndIndex { get { return _endIndex; } }

        public bool IsBuiltin(string name) { return _type == TokenType.Builtin && _str.Equals(name); }
        public bool IsIdentifier(string name) { return _type == TokenType.Identifier && _str.Equals(name); }

        public string Identifier()
        {
            return Identifier(@"Identifier expected.");
        }

        public string Identifier(string errorMessage)
        {
            if (_type != TokenType.Identifier)
                throw new ParseException(errorMessage, _startIndex);
            return _str;
        }

        public void Assert(string token)
        {
            if (!_str.Equals(token))
                throw new ParseException(@"Assertion failed.", _startIndex);
        }

        public override string ToString()
        {
            return _type.ToString() + (_str != null ? ": " + _str : "");
        }
    }

    sealed class TokenJar : IEnumerable<Token>
    {
        private IEnumerator<Token> _enumerator;
        private Token _endToken;

        public TokenJar(IEnumerable<Token> enumerable, Token endToken)
        {
            _enumerator = enumerable.GetEnumerator();
            _endToken = endToken;
        }

        private List<Token> _list;
        public Token this[int index]
        {
            get
            {
                if (_list == null)
                    _list = new List<Token>();
                while (_list.Count <= index)
                {
                    if (!_enumerator.MoveNext())
                        return _endToken;
                    _list.Add(_enumerator.Current);
                }
                return _list[index];
            }
        }

        public bool IndexExists(int index)
        {
            if (_list == null)
                _list = new List<Token>();
            if (_list.Count > index && _list[index].Type != TokenType.EndOfFile)
                return true;
            try
            {
                var token = this[index];
                return token.Type != TokenType.EndOfFile;
            }
            catch (ParseException) { return false; }
        }

        public IEnumerator<Token> GetEnumerator()
        {
            while (_enumerator.MoveNext())
                _list.Add(_enumerator.Current);
            return _list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
