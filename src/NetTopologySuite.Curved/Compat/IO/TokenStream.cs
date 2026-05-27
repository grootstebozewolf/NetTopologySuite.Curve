using System;
using System.Collections.Generic;
using RTools_NTS.Util;

namespace NetTopologySuite.Curved.Compat.IO
{
    /// <summary>
    /// Fork-local copy of <c>NetTopologySuite.Utilities.TokenStream</c>.
    /// </summary>
    /// <remarks>
    /// Upstream <c>develop</c> made <c>TokenStream</c> <see langword="internal"/> and removed the
    /// <c>WKTReader.ReadOtherGeometryText</c> override hook. The composition-based
    /// <see cref="NetTopologySuite.IO.CurveWKTReader"/> drives a token stream directly, so it needs a
    /// public one of its own. TODO(dovetail-9): keep this in sync should the upstream tokenizer change.
    /// </remarks>
    public sealed class TokenStream
    {
        private bool? _prevMoveNextResult;
        private Token _nextToken;

        /// <summary>
        /// Creates an instance wrapping the supplied <paramref name="enumerator"/> of tokens.
        /// </summary>
        public TokenStream(IEnumerator<Token> enumerator) => Enumerator = enumerator;

        /// <summary>
        /// Gets the underlying token enumerator.
        /// </summary>
        public IEnumerator<Token> Enumerator { get; }

        /// <summary>
        /// Returns the next token, optionally advancing the stream.
        /// </summary>
        /// <param name="advance"><see langword="true"/> to consume the token, <see langword="false"/> to peek.</param>
        public Token NextToken(bool advance)
        {
            if (_prevMoveNextResult == null)
                ReadNextToken();

            var result = _nextToken;
            if (advance)
                ReadNextToken();

            return result;
        }

        private void ReadNextToken()
        {
            if (Enumerator.MoveNext())
            {
                _prevMoveNextResult = true;
                _nextToken = Enumerator.Current;
                if (_nextToken == null)
                    throw new InvalidOperationException("Token list contains a null value.");
            }
            else
            {
                _prevMoveNextResult = false;
                _nextToken = null;
            }
        }
    }
}
