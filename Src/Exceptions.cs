using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RT.ParseCs
{
    /// <summary>Provides information about a parse failure.</summary>
    public sealed class ParseException : Exception
    {
        private int _index;
        private object _incompleteResult;

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     The error message.</param>
        /// <param name="index">
        ///     The character index at which the parse error occurred.</param>
        public ParseException(string message, int index) : this(message, index, null, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     The error message.</param>
        /// <param name="index">
        ///     The character index at which the parse error occurred.</param>
        /// <param name="incompleteResult">
        ///     A possible incompletely-parsed tree.</param>
        public ParseException(string message, int index, object incompleteResult) : this(message, index, incompleteResult, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     The error message.</param>
        /// <param name="index">
        ///     The character index at which the parse error occurred.</param>
        /// <param name="incompleteResult">
        ///     A possible incompletely-parsed tree.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public ParseException(string message, int index, object incompleteResult, Exception inner)
            : base(message, inner)
        {
            _index = index;
            _incompleteResult = incompleteResult;
        }

        /// <summary>Returns the index at which the parse error occurred.</summary>
        public int Index { get { return _index; } }

        /// <summary>Returns a possible incompletely-parsed tree.</summary>
        public object IncompleteResult { get { return _incompleteResult; } }
    }

    sealed class InternalErrorException : Exception
    {
        public InternalErrorException(string message = null, Exception inner = null) : base(message, inner) { }
    }
}
