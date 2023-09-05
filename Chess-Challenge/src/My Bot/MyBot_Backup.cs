using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Challenge.src.My_Bot
{
    internal class MyBot_Backup
    {
    /// <summary>
    /// Max Size: 256 MB <br/>
    /// Items: (256 * 1024 * 1024) Bytes / 32 Bytes = 8_388_608
    /// </summary>
    Hash[] _transpositionTable = new Hash[8_388_608];

        /// <summary>
        /// Size: 32B
        /// </summary>
        struct Hash
        {
            public int
                Flag,
                Evaluation,
                Depth;

            public ulong
                Zobrist;

            public Move
                PvMove;
        }

        /*
        /// <summary>
        /// Size: 16 Bytes (?)
        /// </summary>
        struct Hash
        {
            /// <summary>
            /// Zobrist key
            /// </summary>
            public ulong Zobrist;

            /// <summary>
            /// Flag:
            /// 11111111_00000000_00000000_00000000 from ValueCollection:int <br/>
            /// 0: Not in use <br/>
            /// 0x1000000: Exact Evaluation <br/>
            /// 0x2000000: Bigger then beta <br/>
            /// 0x3000000: Smaller than alpha <br/>
            /// <br/>
            /// Depth:
            /// 00000000_11111111_00000000_00000000 from ValueCollection:int <br/>
            /// [0, 255] <br/>
            /// <br/>
            /// PvMove:
            /// 00000000_00000000_11111111_11111111 from ValueCollection:int <br/>
            /// The RawValue of a move struct
            /// </summary>
            public int ValueCollection;

            /// <summary>
            /// Evaluation
            /// </summary>
            public int Evaluation;

            /// <summary>
            /// 11111111_00000000_00000000_00000000 from ValueCollection:int <br/>
            /// 0: Not in use <br/>
            /// 0x1000000: Exact Evaluation <br/>
            /// 0x2000000: Bigger then beta <br/>
            /// 0x3000000: Smaller than alpha
            /// </summary>
            public long Flag 
                => ValueCollection & 0xFF000000;

            /// <summary>
            /// 00000000_11111111_00000000_00000000 from ValueCollection:int <br/>
            /// [0<<16, 255<<16]
            /// </summary>
            public int Depth 
                => ValueCollection & 0x00FF0000;

            /// <summary>
            /// 00000000_00000000_11111111_11111111 from ValueCollection:int <br/>
            /// The RawValue of a move struct
            /// </summary>
            public ushort PvMove
                => (ushort)(ValueCollection & 0x0000FFFF);
        }
        */

        List<int> _nodesSearched = new(); //#DEBG

        Move _resultMove = Move.NullMove;
        Move ResultMove
        {
            get => _resultMove;
            set
            {
                _resultMove = value;
                Console.WriteLine("New ResultMove: " + _resultMove);
            }
        }
        int _currentSearchDepth;

        Board _board;
        Timer _timer;

        /// <summary>
        ///  [Used time]  >  [Total time]  /  [Avarage moves in a game]
        /// </summary>
        bool NotEnoughTime => _timer.MillisecondsElapsedThisTurn > _timer.MillisecondsRemaining / 30;


        public Move Think(Board board, Timer timer)
        {
            _timer = timer;
            _board = board;
            _resultMove = Move.NullMove;

            Console.WriteLine(); //#DEBG
            Console.WriteLine(StaticEvaluation()); //#DEBG


            //_currentSearchDepth = 5;
            //var moves = _board.GetLegalMoves();
            //foreach (var move in moves)
            //{
            //    _board.MakeMove(move);
            //    Console.WriteLine(move + ", " + Search(_currentSearchDepth, -3_765_000, 3_765_000));
            //    _board.UndoMove(move);
            //}

            for (_currentSearchDepth = 0; _currentSearchDepth < 50 && !NotEnoughTime; _currentSearchDepth += 2)
            {
                Console.WriteLine(); //#DEBUG
                Console.WriteLine($"Depth: {_currentSearchDepth}, ");//#DEBUG
                Console.WriteLine($"Eval: {Search(_currentSearchDepth, -3_765_000, 3_765_000)}, ");
                Console.WriteLine($"Best move: {ResultMove}");
                Console.WriteLine(); //#DEBUG
            }

            /* MEMORY MANAGING
            int sizeOfHashInBytes = (int)ObjectSizeHelper.GetSize(this) + _transpositionTable.Length * Marshal.SizeOf(typeof(Hash)); //#DEBUG
            double sizeOfHashInMB = (double)sizeOfHashInBytes / (1024 * 1024); //#DEBUG
            Console.WriteLine($"Memory usage: {sizeOfHashInBytes} ({sizeOfHashInMB}mb)"); //#DEBUG
            Console.WriteLine($"Size of Hash: {Marshal.SizeOf(typeof(Hash))}"); //#DEBUG
            var moves = _board.GetLegalMoves();
            */

            Console.WriteLine($"Searched {_nodesSearched.Count} nodes in {_timer.MillisecondsElapsedThisTurn} with an avarage depth of {(float)_nodesSearched.Sum() / (float)_nodesSearched.Count}"); //#DEBUG

            return ResultMove.IsNull ? _board.GetLegalMoves()[0] : ResultMove;
        }


        public int SearchGoof(int depth, int alpha, int beta)
        {
            ulong key = _board.ZobristKey,
                ttIndex = key % 8_388_608;
            bool qsearch = depth <= 0;
            bool notRoot = _currentSearchDepth - depth > 0;
            int best = -3_765_000;

            // Check for repetition (this is much more important than material and 50 move rule draws)
            if (notRoot && _board.IsRepeatedPosition())
                return 0;

            Hash entry = _transpositionTable[ttIndex];

            // TT cutoffs
            if (notRoot
                && entry.Zobrist == key
                && entry.Depth >= depth
                && (entry.Flag == 3 // exact score
                    || entry.Flag == 2 && entry.Evaluation >= beta // lower bound, fail high
                    || entry.Flag == 1 && entry.Evaluation <= alpha)) // upper bound, fail low
                return entry.Evaluation;


            // Quiescence search is in the same function as negamax to save tokens
            if (qsearch)
            {
                int eval = StaticEvaluation();

                best = eval;
                if (best >= beta)
                    return best;

                alpha = Math.Max(alpha, best);
            }

            // Generate moves, only captures in qsearch
            Move[] moves = _board.GetLegalMoves(qsearch);
            int[] scores = new int[moves.Length];

            // Score moves
            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];

                // TT move
                if (move == entry.PvMove)
                    scores[i] = 3_765_000;

                // MVV-LVA
                else if (move.IsCapture)
                    scores[i] = 100 * (int)move.CapturePieceType - (int)move.MovePieceType;
            }

            Move bestMove = Move.NullMove;
            int origAlpha = alpha;

            // Search moves
            for (int i = 0; i < moves.Length; i++)
            {
                if (NotEnoughTime)
                    return 3_765_000;

                // Incrementally sort moves
                for (int j = i + 1; j < moves.Length; j++)
                {
                    if (scores[j] > scores[i])
                        (scores[i], scores[j], moves[i], moves[j]) = (scores[j], scores[i], moves[j], moves[i]);
                }

                Move move = moves[i];
                _board.MakeMove(move);
                int score = -SearchGoof(depth - 1, -beta, -alpha);
                _board.UndoMove(move);


                // New best move
                if (score > best)
                {
                    best = score;
                    bestMove = move;
                    if (_currentSearchDepth - depth == 0)
                        ResultMove = move;

                    // Improve alpha
                    alpha = Math.Max(alpha, score);

                    // Fail-high
                    if (alpha >= beta)
                        break;
                }
            }

            // (Check/Stale)mate
            if (!qsearch && moves.Length == 0)
                return _board.IsInCheck() ? depth - 3_000_000 : 0;

            // Did we fail high/low or get an exact score?
            int bound =
                best >= beta
                ? 2
                : best > origAlpha
                    ? 3
                    : 1;

            // Push to TT
            _transpositionTable[ttIndex] = new Hash { Zobrist = key, PvMove = bestMove, Depth = depth, Evaluation = best, Flag = bound };

            return best;
        }

        /// <summary>
        /// Principal variation search
        /// </summary>
        /// <param name="depth">remaining depth</param>
        /// <param name="alpha">min</param>
        /// <param name="beta">max</param>
        /// <returns>Evaluation</returns>
        int Search(int depth, int alpha, int beta)
        {
            _nodesSearched.Add(_currentSearchDepth - depth); //#DEBUG

            ulong
                zobrist = _board.ZobristKey,
                ttIndex = zobrist % 8_388_608;
            bool
                notRoot = _currentSearchDepth - depth > 0,
                doQuies = depth <= 0;
            int bestEval = -3_765_000,
                origAlpha;
            var moves = _board.GetLegalMoves(doQuies);
            // [ TOKEN REDUCTION ] creating a copy for this hash, as
            // accessing the hash by the array index costs ~3 tokens, 
            // which accumulates to a lot
            Hash hash = _transpositionTable[ttIndex];


            // check for repetition
            if (notRoot && _board.IsRepeatedPosition())
                return 0;


            // Check for a transposition
            if (hash.Zobrist == ttIndex
                && hash.Depth >= depth
                && (hash.Flag == 2
                || hash.Flag == 3 && hash.Evaluation >= beta
                || hash.Flag == 1 && hash.Evaluation <= alpha)
                && notRoot)
                return hash.Evaluation;


            // perfrom a quiescent search if the max depth is reached or we ran out of time
            if (doQuies)
            {
                bestEval = StaticEvaluation();

                if (bestEval >= beta)
                    return bestEval;

                alpha = Math.Max(alpha, bestEval);
            }


            // fail-soft 
            //if (notRoot && !doQuies)
            //{
            //    bestEval = -Search(depth - 1, -beta, -alpha);
            //    if (bestEval > alpha)
            //    {
            //        if (bestEval >= beta)
            //            return bestEval;

            //        alpha = bestEval;
            //    }
            //}


            // Score moves
            int[] scores = new int[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];

                // TT move
                if (move == hash.PvMove)
                    scores[i] = 3_765_000;

                // TODO
                // mvvlva
                else if (move.IsCapture)
                    scores[i] = GetPieceValueMG((int)move.CapturePieceType) - GetPieceValueMG((int)move.MovePieceType);
            }


            // iterate moves
            origAlpha = alpha;
            Move bestMove = Move.NullMove;
            for (int i = 0; i < moves.Length; i++)
            {
                if (NotEnoughTime)
                    return 3_765_000;


                // sort moves while iterating them
                for (int j = i + 1; j < moves.Length; j++)
                {
                    if (scores[j] > scores[i])
                        (scores[i], scores[j], moves[i], moves[j]) = (scores[j], scores[i], moves[j], moves[i]);
                }

                var move = moves[i];
                int eval;

                _board.MakeMove(move);
                eval = -Search(depth - 1, -beta, -alpha);
                _board.UndoMove(move);

                //// get the pvs evaluation
                //// zero window search
                //_board.MakeMove(move);
                //eval = -Search(depth - 1, doQuies ? -beta : -alpha - 1, -alpha);
                //_board.UndoMove(move);

                //// research if we did not fail high or low
                //if (eval > alpha && eval < beta && depth > 1 && !doQuies)
                //    // research with complete window
                //    eval = Math.Max(eval, Search(depth - 1, -beta, -alpha));

                // after the research, eval could have changed and not be bigger than alpha
                // anymore, so we have to be conditional here

                // new best
                if (eval > bestEval)
                {
                    bestMove = move;
                    bestEval = eval;

                    alpha = Math.Max(alpha, eval);

                    if (depth == _currentSearchDepth)
                        ResultMove = bestMove;

                    // beta cutoff
                    if (alpha >= beta) break;
                }
            }


            // this has to be checked at the end or with the quies search in mind,
            // otherwise it would catch the quies search aswell.
            if (moves.Length == 0 && !doQuies)
                // return if the game has reached an end state
                return _board.IsInCheck()

                    // this player got mated, so return a huge punishment.
                    // in order to avoid repeated mate in one threads, give 
                    // a better score to faster mates.
                    // This value has to be less than the initial alpha.
                    ? depth - 3_000_000

                    // If the game ended and it is no checkmate, we can assume it
                    // is a draw.
                    : -1;


            // Chache the evaluation of this node in the transposition table
            _transpositionTable[ttIndex] = new Hash
            {
                Zobrist = zobrist,
                Evaluation = bestEval,
                Depth = depth,
                PvMove = bestMove,
                Flag = bestEval >= beta ? 3 : bestEval > origAlpha ? 2 : 1
            };


            return bestEval;
        }


        int StaticEvaluation()
        {
            int phase = (Math.Clamp(
                NonPawnPieceValueBonusMG(true) +
                NonPawnPieceValueBonusMG(false), 3915, 15258) - 3915) * 128 / 11343,

                // interpolate between middle and end game evaluations
                result = 28 +
                (GameStateEval(1) * phase +
                (GameStateEval(0) * (128 - phase))) / 128;

            return _board.IsWhiteToMove ? result : -result;
        }

        /// <summary>
        /// Relative to the white player
        /// </summary>
        /// <param name="mg">1 if true, 0 if false</param>
        /// <returns></returns>
        int GameStateEval(int mg)
        {
            var result =

                // Psqa pawns + Piece boni pawns
                _board.GetPieceList(PieceType.Pawn, true).Sum(piece => GetPsqv(mg, piece.Square.Index - 8, 1)) -
                _board.GetPieceList(PieceType.Pawn, false).Sum(piece => GetPsqv(mg, (piece.Square.Index ^ 56) - 8, 1));

            // Psqa pieces + Piece boni pieces
            for (int i = 2; i < 6; i++)
                result += AccumulatePsqa(true, i, mg) - AccumulatePsqa(false, i, mg);

            return result;
        }
        int AccumulatePsqa(bool white, int pieceType, int mg)
            => _board.GetPieceList((PieceType)pieceType, white)
            .Sum(piece => GetPsqv(mg, MapSquareIndexToPsqa(piece.Square.Index, white), pieceType));


        /// <summary>
        /// 0 -> 0, <br/>
        /// 1 -> 1, <br/> 
        /// 2 -> 2, <br/> 
        /// 3 -> 3, <br/> 
        /// 4 -> 3, <br/> 
        /// 5 -> 2, <br/> 
        /// 6 -> 1, <br/> 
        /// 7 -> 0, <br/> 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        int MapSquareIndexToPsqa(int index, bool white)
        {
            int file = index & 7;
            //-----------row----------------//  +  //---------file----------//
            return (index / 8 * 4 ^ (white ? 0 : 0x1C)) + (file < 4 ? file : 7 - file);
        }

        /// <summary>
        /// piece square array + piece boni <br/>
        /// mg1_eg1__mg2_eg2
        /// </summary>
        /*
        ulong[] psqs =
        {        
            // Pawn
            // length: 24
                0b0000000001111110_0000000011000110__0000000010000000_0000000011001000, 0b0000000010000111_0000000011010111__0000000010001110_0000000011010011, 0b0000000010001100_0000000011011110__0000000010010001_0000000011010100, 0b0000000010000101_0000000011001000__0000000001111001_0000000010111100,
                0b0000000001110011_0000000011000101__0000000001101101_0000000011000111, 0b0000000010000111_0000000011000100__0000000010001011_0000000011010011, 0b0000000010011011_0000000011010000__0000000010010011_0000000011010001, 0b0000000010000010_0000000011000110__0000000001101000_0000000011001001,
                0b0000000001111001_0000000011010101__0000000001101000_0000000011001111, 0b0000000010000100_0000000011000110__0000000010001111_0000000011001100, 0b0000000010100011_0000000011000000__0000000010001101_0000000011000001, 0b0000000001111110_0000000011000011__0000000001110111_0000000011001000,
                0b0000000010000111_0000000011011010__0000000001111000_0000000011010100, 0b0000000001110001_0000000011010000__0000000001111110_0000000011001000, 0b0000000010000111_0000000011001001__0000000001111100_0000000011001010, 0b0000000001110000_0000000011011100__0000000010000001_0000000011010111,
                0b0000000001111111_0000000011101001__0000000001110001_0000000011100000, 0b0000000001110110_0000000011100001__0000000010010010_0000000011101011, 0b0000000001110100_0000000011101100__0000000001110111_0000000011010111, 0b0000000001101110_0000000011010110__0000000001110001_0000000011011100,
                0b0000000001110101_0000000011001101__0000000010000010_0000000011000000, 0b0000000001111010_0000000011011011__0000000001110001_0000000011100100, 0b0000000010000000_0000000011100110__0000000001101110_0000000011011111, 0b0000000010000110_0000000011010101__0000000001110011_0000000011010101,

            // Knight
            // length: 16
                0b0000001001011110_0000001011110110__0000001010110001_0000001100010101, 0b0000001011000011_0000001100100101__0000001011000100_0000001101000001, 0b0000001011000000_0000001100010011__0000001011100100_0000001100100000, 0b0000001011110010_0000001101000100__0000001011111110_0000001101011110,
                0b0000001011010000_0000001100101110__0000001011111100_0000001100111011, 0b0000001100010011_0000001101001110__0000001100011001_0000001101110011, 0b0000001011101010_0000001100110011__0000001100010101_0000001101010100, 0b0000001100110101_0000001101100011__0000001100111110_0000001101110010,
                0b0000001011101011_0000001100101001__0000001100011010_0000001101000110, 0b0000001100111001_0000001101011111__0000001101000000_0000001101111101, 0b0000001100000100_0000001100100011__0000001100100011_0000001100101010, 0b0000001101000111_0000001101000110__0000001101000010_0000001101100111,
                0b0000001011001010_0000001100010001__0000001011110010_0000001100100100, 0b0000001100010001_0000001100100011__0000001100110010_0000001101100010, 0b0000001001000100_0000001011110010__0000001010111010_0000001011111110, 0b0000001011010101_0000001100011110__0000001011110011_0000001101000101,

            // Bishop
            // length: 16
                0b0000001100010100_0000001101101011__0000001100110101_0000001101111110, 0b0000001100110011_0000001101111001__0000001100101001_0000001110001011, 0b0000001100101110_0000001101111001__0000001100111111_0000001110001010, 0b0000001101000110_0000001110000111__0000001100111100_0000001110010100,
                0b0000001100110100_0000001110001000__0000001101001000_0000001110010010, 0b0000001100110101_0000001110010010__0000001101000101_0000001110011010, 0b0000001100110101_0000001110000101__0000001101000001_0000001110001111, 0b0000001101001011_0000001110010011__0000001101010100_0000001110011111,
                0b0000001100110001_0000001110000111__0000001101001101_0000001110010010, 0b0000001101001000_0000001110001001__0000001101001111_0000001110011110, 0b0000001100101110_0000001101111110__0000001100111101_0000001110010111, 0b0000001100111010_0000001110010110__0000001101000001_0000001110010111,
                0b0000001100101101_0000001101111101__0000001100101111_0000001110000101, 0b0000001100111101_0000001110010010__0000001100111001_0000001110010100, 0b0000001100010111_0000001101110011__0000001100111010_0000001101110110, 0b0000001100101111_0000001101111001__0000001100101001_0000001110000010,

            // Rook
            // length: 16
                0b0000010011011101_0000010101011011__0000010011101000_0000010101010111, 0b0000010011101110_0000010101011010__0000010011110111_0000010101011011, 0b0000010011100111_0000010101011000__0000010011101111_0000010101011011, 0b0000010011110100_0000010101100011__0000010100000010_0000010101100010,
                0b0000010011100011_0000010101101010__0000010011110001_0000010101011100, 0b0000010011111011_0000010101100010__0000010011111111_0000010101011110, 0b0000010011101111_0000010101011110__0000010011110111_0000010101100101, 0b0000010011111000_0000010101011011__0000010011110110_0000010101101011,
                0b0000010011100001_0000010101011111__0000010011101101_0000010101101100, 0b0000010011111000_0000010101101011__0000010011111111_0000010101011110, 0b0000010011100110_0000010101101010__0000010011111010_0000010101100101, 0b0000010100000010_0000010101011101__0000010100001000_0000010101101110,
                0b0000010011111010_0000010101101000__0000010100001000_0000010101101001, 0b0000010100001100_0000010101111000__0000010100001110_0000010101011111, 0b0000010011101011_0000010101110110__0000010011101001_0000010101100100, 0b0000010011111011_0000010101110111__0000010100000101_0000010101110001,

            // Queen
            // length: 16
                0b0000100111101101_0000101000110101__0000100111100101_0000101001000001, 0b0000100111100101_0000101001001011__0000100111101110_0000101001100000, 0b0000100111100111_0000101001000100__0000100111101111_0000101001011011, 0b0000100111110010_0000101001100100__0000100111110110_0000101001110110,
                0b0000100111100111_0000101001010011__0000100111110000_0000101001101000, 0b0000100111110111_0000101001110001__0000100111110001_0000101001111101, 0b0000100111101110_0000101001100011__0000100111101111_0000101001110111, 0b0000100111110011_0000101010000111__0000100111110010_0000101010010010,
                0b0000100111101010_0000101001011101__0000100111111000_0000101001110100, 0b0000100111110110_0000101010000011__0000100111101111_0000101010001111, 0b0000100111100110_0000101001010100__0000100111110100_0000101001101000, 0b0000100111110000_0000101001101111__0000100111110010_0000101001111011,
                0b0000100111100101_0000101001001000__0000100111110000_0000101001011111, 0b0000100111110100_0000101001100010__0000100111110010_0000101001110010, 0b0000100111101000_0000101000110000__0000100111101000_0000101001000110, 0b0000100111101011_0000101001001111__0000100111101000_0000101001011000,

            // King
            // length: 16
                0b0000110011000111_0000101110111001__0000110011111111_0000101111100101, 0b0000110011000111_0000110000001101__0000110001111110_0000110000000100, 0b0000110011001110_0000101111101101__0000110011100111_0000110000011100, 0b0000110010100010_0000110000111101__0000110001101011_0000110000111111,
                0b0000110001111011_0000110000010000__0000110010111010_0000110000111010, 0b0000110001100001_0000110001100001__0000110000110000_0000110001100111, 0b0000110001011100_0000110000011111__0000110001110110_0000110001010100, 0b0000110001000010_0000110001100100__0000110000011010_0000110001100100,
                0b0000110001010010_0000110000011000__0000110001101011_0000110001011110, 0b0000110000100001_0000110001111111__0000101111111110_0000110001111111, 0b0000110000110011_0000110000010100__0000110001001001_0000110001100100, 0b0000110000001001_0000110001110000__0000101111010111_0000110001110111,
                0b0000110000010000_0000101111100111__0000110000110000_0000110000110001, 0b0000101111111001_0000110000101100__0000101111011001_0000110000111011, 0b0000101111110011_0000101111000011__0000110000010001_0000101111110011, 0b0000101111100101_0000110000000001__0000101110110111_0000110000000110,
        };
        */
        ulong[][] _psqa =
        {
        // Pawn
        // length: 48
        new ulong[]
        {
            0b0000000001111110_0000000011000110__0000000010000000_0000000011001000, 0b0000000010000111_0000000011010111__0000000010001110_0000000011010011, 0b0000000010001100_0000000011011110__0000000010010001_0000000011010100, 0b0000000010000101_0000000011001000__0000000001111001_0000000010111100,
            0b0000000001110011_0000000011000101__0000000001101101_0000000011000111, 0b0000000010000111_0000000011000100__0000000010001011_0000000011010011, 0b0000000010011011_0000000011010000__0000000010010011_0000000011010001, 0b0000000010000010_0000000011000110__0000000001101000_0000000011001001,
            0b0000000001111001_0000000011010101__0000000001101000_0000000011001111, 0b0000000010000100_0000000011000110__0000000010001111_0000000011001100, 0b0000000010100011_0000000011000000__0000000010001101_0000000011000001, 0b0000000001111110_0000000011000011__0000000001110111_0000000011001000,
            0b0000000010000111_0000000011011010__0000000001111000_0000000011010100, 0b0000000001110001_0000000011010000__0000000001111110_0000000011001000, 0b0000000010000111_0000000011001001__0000000001111100_0000000011001010, 0b0000000001110000_0000000011011100__0000000010000001_0000000011010111,
            0b0000000001111111_0000000011101001__0000000001110001_0000000011100000, 0b0000000001110110_0000000011100001__0000000010010010_0000000011101011, 0b0000000001110100_0000000011101100__0000000001110111_0000000011010111, 0b0000000001101110_0000000011010110__0000000001110001_0000000011011100,
            0b0000000001110101_0000000011001101__0000000010000010_0000000011000000, 0b0000000001111010_0000000011011011__0000000001110001_0000000011100100, 0b0000000010000000_0000000011100110__0000000001101110_0000000011011111, 0b0000000010000110_0000000011010101__0000000001110011_0000000011010101,
        },

        // Knight
        new ulong[]
        {
            0b0000001001011110_0000001011110110__0000001010110001_0000001100010101, 0b0000001011000011_0000001100100101__0000001011000100_0000001101000001, 0b0000001011000000_0000001100010011__0000001011100100_0000001100100000, 0b0000001011110010_0000001101000100__0000001011111110_0000001101011110,
            0b0000001011010000_0000001100101110__0000001011111100_0000001100111011, 0b0000001100010011_0000001101001110__0000001100011001_0000001101110011, 0b0000001011101010_0000001100110011__0000001100010101_0000001101010100, 0b0000001100110101_0000001101100011__0000001100111110_0000001101110010,
            0b0000001011101011_0000001100101001__0000001100011010_0000001101000110, 0b0000001100111001_0000001101011111__0000001101000000_0000001101111101, 0b0000001100000100_0000001100100011__0000001100100011_0000001100101010, 0b0000001101000111_0000001101000110__0000001101000010_0000001101100111,
            0b0000001011001010_0000001100010001__0000001011110010_0000001100100100, 0b0000001100010001_0000001100100011__0000001100110010_0000001101100010, 0b0000001001000100_0000001011110010__0000001010111010_0000001011111110, 0b0000001011010101_0000001100011110__0000001011110011_0000001101000101,
        },

        // Bishop
        new ulong[]
        {
            0b0000001100010100_0000001101101011__0000001100110101_0000001101111110, 0b0000001100110011_0000001101111001__0000001100101001_0000001110001011, 0b0000001100101110_0000001101111001__0000001100111111_0000001110001010, 0b0000001101000110_0000001110000111__0000001100111100_0000001110010100,
            0b0000001100110100_0000001110001000__0000001101001000_0000001110010010, 0b0000001100110101_0000001110010010__0000001101000101_0000001110011010, 0b0000001100110101_0000001110000101__0000001101000001_0000001110001111, 0b0000001101001011_0000001110010011__0000001101010100_0000001110011111,
            0b0000001100110001_0000001110000111__0000001101001101_0000001110010010, 0b0000001101001000_0000001110001001__0000001101001111_0000001110011110, 0b0000001100101110_0000001101111110__0000001100111101_0000001110010111, 0b0000001100111010_0000001110010110__0000001101000001_0000001110010111,
            0b0000001100101101_0000001101111101__0000001100101111_0000001110000101, 0b0000001100111101_0000001110010010__0000001100111001_0000001110010100, 0b0000001100010111_0000001101110011__0000001100111010_0000001101110110, 0b0000001100101111_0000001101111001__0000001100101001_0000001110000010,
        },

        // Rook
        new ulong[]
        {
            0b0000010011011101_0000010101011011__0000010011101000_0000010101010111, 0b0000010011101110_0000010101011010__0000010011110111_0000010101011011, 0b0000010011100111_0000010101011000__0000010011101111_0000010101011011, 0b0000010011110100_0000010101100011__0000010100000010_0000010101100010,
            0b0000010011100011_0000010101101010__0000010011110001_0000010101011100, 0b0000010011111011_0000010101100010__0000010011111111_0000010101011110, 0b0000010011101111_0000010101011110__0000010011110111_0000010101100101, 0b0000010011111000_0000010101011011__0000010011110110_0000010101101011,
            0b0000010011100001_0000010101011111__0000010011101101_0000010101101100, 0b0000010011111000_0000010101101011__0000010011111111_0000010101011110, 0b0000010011100110_0000010101101010__0000010011111010_0000010101100101, 0b0000010100000010_0000010101011101__0000010100001000_0000010101101110,
            0b0000010011111010_0000010101101000__0000010100001000_0000010101101001, 0b0000010100001100_0000010101111000__0000010100001110_0000010101011111, 0b0000010011101011_0000010101110110__0000010011101001_0000010101100100, 0b0000010011111011_0000010101110111__0000010100000101_0000010101110001,
        },

        // Queen
        new ulong[]
        {
            0b0000100111101101_0000101000110101__0000100111100101_0000101001000001, 0b0000100111100101_0000101001001011__0000100111101110_0000101001100000, 0b0000100111100111_0000101001000100__0000100111101111_0000101001011011, 0b0000100111110010_0000101001100100__0000100111110110_0000101001110110,
            0b0000100111100111_0000101001010011__0000100111110000_0000101001101000, 0b0000100111110111_0000101001110001__0000100111110001_0000101001111101, 0b0000100111101110_0000101001100011__0000100111101111_0000101001110111, 0b0000100111110011_0000101010000111__0000100111110010_0000101010010010,
            0b0000100111101010_0000101001011101__0000100111111000_0000101001110100, 0b0000100111110110_0000101010000011__0000100111101111_0000101010001111, 0b0000100111100110_0000101001010100__0000100111110100_0000101001101000, 0b0000100111110000_0000101001101111__0000100111110010_0000101001111011,
            0b0000100111100101_0000101001001000__0000100111110000_0000101001011111, 0b0000100111110100_0000101001100010__0000100111110010_0000101001110010, 0b0000100111101000_0000101000110000__0000100111101000_0000101001000110, 0b0000100111101011_0000101001001111__0000100111101000_0000101001011000,
        },

        // King
        new ulong[]
        {
            0b0000110011000111_0000101110111001__0000110011111111_0000101111100101, 0b0000110011000111_0000110000001101__0000110001111110_0000110000000100, 0b0000110011001110_0000101111101101__0000110011100111_0000110000011100, 0b0000110010100010_0000110000111101__0000110001101011_0000110000111111,
            0b0000110001111011_0000110000010000__0000110010111010_0000110000111010, 0b0000110001100001_0000110001100001__0000110000110000_0000110001100111, 0b0000110001011100_0000110000011111__0000110001110110_0000110001010100, 0b0000110001000010_0000110001100100__0000110000011010_0000110001100100,
            0b0000110001010010_0000110000011000__0000110001101011_0000110001011110, 0b0000110000100001_0000110001111111__0000101111111110_0000110001111111, 0b0000110000110011_0000110000010100__0000110001001001_0000110001100100, 0b0000110000001001_0000110001110000__0000101111010111_0000110001110111,
            0b0000110000010000_0000101111100111__0000110000110000_0000110000110001, 0b0000101111111001_0000110000101100__0000101111011001_0000110000111011, 0b0000101111110011_0000101111000011__0000110000010001_0000101111110011, 0b0000101111100101_0000110000000001__0000101110110111_0000110000000110,
        },
    };

        short GetPsqv(int mg, int index, int piece)
            => ExtractShort(_psqa[--piece][index / 2], mg + (index & 1) * 2);



        /* [ TOKEN REDUCTION ] MERGED INTO `GetPsqv`
        /// <summary>
        /// </summary>
        /// <param name="mg"></param>
        /// <param name="rank"></param>
        /// <param name="file"></param>
        /// <param name="piece"></param>
        /// <returns></returns>
        short GetPsqv(int mg, int index, int piece)
            => (short)(_psqa[--piece][index >> 1] >> (48 - mg - (index & 1) * 2 * 16) & 0xFFFFul);

        */
        /// <summary>
        /// Extract an Int16 form an uInt64 at a given index.
        /// </summary>
        /// <param name="u64">source</param>
        /// <param name="index">[0,3]</param>
        /// <returns></returns>
        short ExtractShort(ulong u64, int index) => (short)(u64 >> (48 - index * 16) & 0xFFFFul);


        /* [ TOKEN REDUCTION ] MERGED INTO PSQA 
         * only used by the phase calculation, if token limit is reached
         * this could be replaced by something more efficient.
         */
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mg"></param>
        /// <param name="white"></param>
        /// <param name="valuePawns"></param>
        /// <returns>The piece boni for the given color</returns>
        int NonPawnPieceValueBonusMG(bool white)
        {
            int result = 0;

            for (int i = 2; i < 6; i++)
                result += BitboardHelper.GetNumberOfSetBits(_board.GetPieceBitboard((PieceType)i, white)) * GetPieceValueMG(i);

            return result;
        }

        short GetPieceValueMG(int piece) => ExtractShort(_pieceValues[(piece - 1) / 2], (~piece & 1) * 2);

        /// <summary>
        /// (mg1_eg1__mg2_eg2)
        /// </summary>
        ulong[] _pieceValues =
        {
        /*pawn__knight*/
        0x007C_00CE__030D_0356,

        /*bishop__rook*/
        0x0339_0393__04FC_0564,

        /*queen__king*/
        0x09EA_0A7A__0BB8_0BB8,
    };

        //int PieceValueBonus(bool mg, bool white, bool valuePawns)
        //{
        //    short[] pieceValues = mg 
        //        ? new short[] { 124, 781, 825, 1276, 2538, 3000 } 
        //        : new short[] { 206, 854, 915, 1380, 2682, 3000 };

        //    int result = 0;
        //    for (int i = valuePawns ? 1 : 2; i < 6; i++)
        //        result += BitboardHelper.GetNumberOfSetBits(_board.GetPieceBitboard((PieceType)i, white)) * pieceValues[i - 1];

        //    return result;
        //}

    }
}
