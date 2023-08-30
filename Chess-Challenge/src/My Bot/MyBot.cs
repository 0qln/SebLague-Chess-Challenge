using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq; // do not use ```AsParallel()```

// =============
// backup branch
// =============

// not backup branch

public class MyBot : IChessBot
{
    // Constants
    // ===
    int PV_COUNT = 100; // high value, search all lines
    int STACK_DUMMIES = 6;

    int[] PhaseValues = 
    { 
        0, 155, 305, 405, 1050, 0 
    };
    ulong[] CompressedSTablesNorm =
    {
        50947748185571509, 50947748185571509, 77406597867307265, 44473961167651086, 54606948651827363, 43347966767071463, 49540300287115412, 40533169751785650,
        47851437540704392, 38281331283001519, 42221886466621576, 41377603271131293, 39688603087208581, 39688822131523734, 50947748185571509, 50947748185571509,
        98799228674179269, 76562735577825699, 115969554442092850, 101895577980699026, 124695235777462611, 120473248570606038, 124132208513188221, 116532444274622894,
        113154684420817275, 108088164905451938, 112591695811051888, 105554868639433112, 108369502438424933, 104991785542156677, 99925068455543073, 102739972841079161,
        102458330364182901, 113717685912011132, 112591708698902925, 105836412339880377, 128073064348516775, 124976891145290177, 130887741098623389, 118784235498439107,
        125821178633978264, 118784269855949254, 120473145486279072, 119628716261900721, 118221358557757854, 116532564532003241, 114280657343349126, 110902785823539615,
        167761525699772990, 162132017573790291, 173109730645049918, 165791183685091955, 162695027657212450, 157910090493985332, 162131978917839377, 150872941197460021,
        155095070139482635, 150591599362507309, 153687695256584714, 150028563510329903, 156783915706155532, 141865827841671732, 157628353520599580, 151717275931574833,
        279227484813591474, 284012606665327637, 282042152973894599, 287390263436379081, 281479220202701783, 294708793221252089, 274442302834213826, 280353316002726867,
        275005261377504220, 279227441864180697, 276694115534832592, 280916343264445401, 278383042703393723, 277820062686839780, 281197740930892763, 264309122068579283,
        31525781517041734, 28992364987088940, 32933027548561526, 14074045194109025, 25896226145370226, 22237021384278108, 16888850794676305, 5911257982631997,
        3940881608540216, 5348200652668928, 1126088890581080, 15481372828565506, 2533566854463581, 27584960037978137, 6474396918939723, 30681266386370649,
        50947748185571509, 50947748185571509, 87258673027416422, 103021323260395841, 70088398795571471, 73747564899795180, 51510732499845329, 53762540902351022,
        46725576290336956, 48414439034323111, 48695883947114673, 45881134179156137, 50103310370472124, 46444101312839861, 50947748185571509, 50947748185571509,
        93169720550555958, 75718057246982482, 101895419061272909, 87258707386368354, 104147334838419790, 91199369946333543, 107525073214701916, 97110438947717500,
        107525047444439384, 96266009722618233, 104428758276309331, 94577056781894000, 100769549218218306, 89510507200315749, 97110322979668302, 83317946043400534,
        104991746884567409, 100769622234694006, 103865907108315506, 101895552206242169, 104991785539993973, 105273260517425529, 108650998892790134, 106399160424399235,
        109495419527364978, 103865864159101307, 108088044643877230, 101614060049990019, 106962088901804398, 99080759489528189, 105836167520649574, 103021434933150071,
        164102441196126795, 164383903288394313, 164102415426716234, 163820957629284924, 163539452588196425, 162413539795927621, 163539499832771144, 164946831766782535,
        165228345398526538, 162132064819741257, 164946831766716997, 160443227844051523, 164946840356127297, 163257943251419715, 162413548385993277, 157909944463393342,
        288797724218360807, 287953217683129335, 290486586964050908, 279508985562137639, 294990100690502609, 280353393312662548, 293582781643031535, 290205262311982120,
        295834594341946318, 286264492378883081, 284012606661657568, 282886711053059063, 276975517495919580, 272753405729833944, 268249823281939399, 270220216838587365,
        15762847806390279, 19422125586317382, 25333112981880895, 25051680956153944, 24488713821552718, 23644361907044441, 28429376380534849, 21955439032008799,
        28429376379093048, 18577700656513129, 28429359199486006, 19422129881546856, 26740487863926833, 16607328574242912, 21110872364089370, 9570376846213181
    };


    // Getters/Setters
    // ===

    // [Used_time] > [Total_time] / [Avarage_moves_in_a_game]
    bool EnoughTime => _timer.MillisecondsElapsedThisTurn <= _timer.MillisecondsRemaining / 30;
    // 0: mg | 96: eg
    int GetPieceEval(int piece, int square, int mg) => ExtractShort(CompressedSTablesNorm[piece*4 + square/4 + mg], square%4) - 81;
    int ExtractShort(ulong u64, int index) => (short)(u64 >> (index * 16) & 0xFFFFul);

    int StackIndex(int index) => index + STACK_DUMMIES;

    int Alpha(int value) => Math.Max(value,-(int)Value.VALUE_INFINITE);
    int Beta (int value) => Math.Min(value, (int)Value.VALUE_INFINITE);


    // Fields
    // ===
    Timer _timer;
    Board _board;
    Stack[] _stacks; // indexed by ply
    int[] _rootMoveScoresAVG;
    Move[] _rootMoves;
    TTEntry[] _TT = new TTEntry[1<<22]; //Size ~4_000_000
    int _rootDelta, _rootDepth, _currentRootMove, _bestValue;
    Move _rootBestMove;


    public Move Think(Board board, Timer timer)
    {
        _board = board;
        _timer = timer;
        _rootBestMove = Move.NullMove;

        for (_rootDepth = 0; ++_rootDepth < (int)Value.MAX_PLY;)
        {
            Console.WriteLine($"Depth: {_rootDepth}");   //#DEBUG

            int alpha = -(int)Value.VALUE_INFINITE;
            int beta = (int)Value.VALUE_INFINITE;

            Search_DEBUG_DEBUG(0, alpha, beta, _rootDepth, false, true, false);
            
            // Out of time
            if (!EnoughTime) break;
        }

        return _rootBestMove.IsNull ? board.GetLegalMoves()[0] : _rootBestMove;
    }

    public Move Think_NOT_DEBUG(Board board, Timer timer)
    {
        _timer = timer;
        _board = board;
        _stacks = new Stack[(int)Value.MAX_PLY + STACK_DUMMIES];
        
        _rootMoves = board.GetLegalMoves();
        _rootBestMove = _rootMoves[0];
        _bestValue = -(int)Value.VALUE_INFINITE;

        _rootMoveScoresAVG = new int[_rootMoves.Length];

        // Iterative Deepening
        for (_rootDepth = 0; ++_rootDepth < (int)Value.MAX_PLY;)
        {
            for (_currentRootMove = 0; ++_currentRootMove < _rootMoves.Length;)
            {
                // Init alpha, beta, delta
                int prev = _rootMoveScoresAVG[_currentRootMove];
                int delta = 10 + prev * prev / 15799;
                int alpha = Alpha(prev - delta);
                int beta = Beta(prev + delta);

                alpha = -(int)Value.VALUE_INFINITE;
                beta = (int)Value.VALUE_INFINITE;

                //Search with increasing asp window
                int failHighs = 0;
                while (true)
                {
                    _stacks[StackIndex(0)].CurrentMove = _rootMoves[_currentRootMove];

                    _board.MakeMove(_rootMoves[_currentRootMove]);
                    int newEval = -Search_DEBUG_DEBUG(0, alpha, beta, _rootDepth/* - failHighs * 2*/, false, true, false);
                    _board.UndoMove(_rootMoves[_currentRootMove]);

                    ///_rootMoveScoresAVG[_currentRootMove] = _rootDepth == 0 ? newEval : (newEval * 2 + _rootMoveScoresAVG[_currentRootMove]) / 3;
                    _rootMoveScoresAVG[_currentRootMove] = newEval;
                    if (_rootMoveScoresAVG[_currentRootMove] > _bestValue)
                    {
                        //_bestValue = _rootMoveScoresAVG[_currentRootMove];
                        //_rootBestMove = _stacks[StackIndex(0)].CurrentMove;
                        //Console.WriteLine($"new best: {_rootBestMove}");
                    }

                    // go for forced mate
                    if (_rootMoveScoresAVG[_currentRootMove] >= (int)Value.VALUE_MATE_IN_MAX_PLY)
                    {
                        Console.WriteLine("Mate found. " + _rootMoves[_currentRootMove] + " " + _rootMoveScoresAVG[_currentRootMove]);
                        return _rootMoves[_currentRootMove];
                    }

                    // Check for a fail high/ low
                    if (_rootMoveScoresAVG[_currentRootMove] <= alpha)
                    {
                        beta = (alpha + beta) / 2;
                        alpha = Alpha(_rootMoveScoresAVG[_currentRootMove] - delta);
                        failHighs++;
                    }

                    else if (_rootMoveScoresAVG[_currentRootMove] >= beta)
                    {
                        beta = Beta(_rootMoveScoresAVG[_currentRootMove] + delta);
                        ++failHighs;
                    }

                    else break;


                    delta += (delta / 3);
                }
            }

            // After each search, sort the moves
            // For some reason, `_rootMoveScores` get's sorted aswell (which is neat)
            ///Array.Sort(_rootMoveScoresAVG, _rootMoves);

            if (EnoughTime) { 
                ///_rootBestMove = _rootMoves[^1];

                PrintRootMoves("\n\n"); //#DEBUG
                Console.WriteLine($"Completed Depth: {_rootDepth}"); //#DEBUG
            }

            if (!EnoughTime) break;
        }


        //PrintRootMoves("\n\n"); //#DEBUG
        //Console.WriteLine($"Completed Depth: {_rootDepth}"); //#DEBUG


        // The best move should now be on the bottom,
        // play the last move in the `_rootMoves` array
        _rootBestMove = _rootMoves[Array.IndexOf(_rootMoveScoresAVG, _rootMoveScoresAVG.Max())];

        Console.WriteLine("Play move: " + _rootBestMove);
        return /*_rootMoves[^1]*/ _rootBestMove;
    }

    void PrintRootMoves(string s)   //#DEBUG
    {                               //#DEBUG
        Console.WriteLine(s);       //#DEBUG
        for (int i = 0; i < _rootMoves.Length; i++) //#DEBUG
        {                                           //#DEBUG
            _board.MakeMove(_rootMoves[i]);         //#DEBUG
            Console.WriteLine($"{_rootMoves[i]}: {_rootMoveScoresAVG[i].ToString().PadRight(8)} se: {-StaticEvaluation()}"); //#DEBUG
            _board.UndoMove(_rootMoves[i]); //#DEBUG
        }                                   //#DEBUG
    }                                       //#DEBUG


    struct TTEntry_DEBUG
    {
        public ulong key;
        public Move move;
        public int depth, score, bound;
        public TTEntry_DEBUG(ulong _key, Move _move, int _depth, int _score, int _bound)
        {
            key = _key; move = _move; depth = _depth; score = _score; bound = _bound;
        }
    }

    const int entries = (1 << 20);
    TTEntry_DEBUG[] tt = new TTEntry_DEBUG[entries];

    int Search_DEBUG_DEBUG(int ply, int alpha, int beta, int depth, bool cutNode, bool rootNode, in bool pvNode)
    {
        ulong key = _board.ZobristKey;
        bool qsearch = depth <= 0;
        bool notRoot = ply > 0;
        int best = -30000;

        // Check for repetition (this is much more important than material and 50 move rule draws)
        if (notRoot && _board.IsRepeatedPosition())
            return 0;

        TTEntry_DEBUG entry = tt[key % entries];

        // TT cutoffs
        if (notRoot
            && entry.key == key
            && entry.depth >= depth
            && (entry.bound == 3 // exact score
                || entry.bound == 2 && entry.score >= beta // lower bound, fail high
                || entry.bound == 1 && entry.score <= alpha)) // upper bound, fail low
            return entry.score;


        int eval = StaticEvaluation();

        // Quiescence search is in the same function as negamax to save tokens
        if (qsearch)
        {
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
            if (move == entry.move)
                scores[i] = 1000000;

            // https://www.chessprogramming.org/MVV-LVA
            else if (move.IsCapture)
                scores[i] = 100 * (int)move.CapturePieceType - (int)move.MovePieceType;
        }


        Move bestMove = Move.NullMove;
        int origAlpha = alpha;

        // Search moves
        for (int i = 0; i < moves.Length; i++)
        {
            if (!EnoughTime) return 30000;

            // Incrementally sort moves
            for (int j = i + 1; j < moves.Length; j++)
            {
                if (scores[j] > scores[i])
                    (scores[i], scores[j], moves[i], moves[j]) = (scores[j], scores[i], moves[j], moves[i]);
            }

            Move move = moves[i];
            _board.MakeMove(move);
            int score = -Search_DEBUG_DEBUG(ply+1, -beta, -alpha, depth - 1, false, false, false);
            _board.UndoMove(move);


            // New best move
            if (score > best)
            {
                best = score;
                bestMove = move;
                if (ply == 0)
                    _rootBestMove = move;

                // Improve alpha
                alpha = Math.Max(alpha, score);

                // Fail-high
                if (alpha >= beta)
                    break;
            }
        }

        // (Check/Stale)mate
        if (!qsearch && moves.Length == 0)
            return _board.IsInCheck() ? -30000 + ply : 0;

        // Did we fail high/low or get an exact score?
        int bound =
            best >= beta
            ? 2
            : best > origAlpha
                ? 3
                : 1;

        // Push to TT
        tt[key % entries] = new TTEntry_DEBUG(key, bestMove, depth, best, bound);

        return best;

    }

    int Search_DEBUG(int ply, int alpha, int beta, int depth, bool cutNode, bool rootNode, in bool pvNode)
    {
        if (!rootNode)
        {
            // __Repetition__
            if (_board.FiftyMoveCounter >= 3
                && alpha < (int)Value.VALUE_DRAW
                && _board.IsRepeatedPosition())
            {
                if (alpha >= beta) return (int)Value.VALUE_DRAW;
            }


            // __Immediate Draw Evaluation__
            if (_board.IsDraw() // Check Draw
                || ply >= (int)Value.MAX_PLY)
                return StaticEvaluation_DEBUG();

            // __Check time__
            if (!EnoughTime)
                return 30000;


            // __Bounds Check__
            if (ply >= (int)Value.MAX_PLY - 10
                || depth <= 0)
                return StaticEvaluation_DEBUG();


            // __Mate distance pruning__
        }

        // __Quiescence search__


        // __Transposition table lookup__


        // __[[Static evaluation]] and improvement flag__


        // __Loop through all legal moves until no moves remain__
        int bestValue = -(int)Value.VALUE_INFINITE,
            value;
        Move[] moves = _board.GetLegalMoves();

        foreach (var move in moves)
        {
            // __Make the move__
            _board.MakeMove(move);


            // __Perform the full depth search__
            value = -Search_DEBUG(ply + 1, -beta, -alpha, depth-1, false, false, false);


            // __Undo Move__
            _board.UndoMove(move);


            // __Check for a new best move___
            if (value > bestValue)
            {
                bestValue = value;

                if (value > alpha)
                {
                    if (value >= beta)
                    {
                        // Fail high
                        break;
                    }
                    else
                    {
                        alpha = value;
                    }
                }
            }
        }


        // __Check for mate and stalemate__
        if (moves.Length == 0)
            bestValue = _board.IsInCheck()
                ? -(int)Value.VALUE_INFINITE + ply
                : (int)Value.VALUE_DRAW;


        return bestValue;
    }

    /// <summary>
    /// Recursive search function with quies search.
    /// </summary>
    /// <returns></returns>
    int Search(int ply, int alpha, int beta, int depth, bool cutNode, bool rootNode, in bool pvNode)
    {
        int fBase = 0;
        bool quies, improving;

        _stacks[StackIndex(ply)].InCheck = _board.IsInCheck();


        if (!rootNode)
        {
            // __Repetition__
            if (_board.FiftyMoveCounter >= 3
                && alpha < (int)Value.VALUE_DRAW
                && _board.IsRepeatedPosition())
            {
                alpha = (int)Value.VALUE_DRAW;
                if (alpha >= beta) return alpha;
            }


            // __Immediate Draw Evaluation__
            if (_board.IsDraw() // Check Draw
                || ply >= (int)Value.MAX_PLY)
                return StaticEvaluation();

            // __Check time__
            if (!EnoughTime)
                return 30000;


            // __Bounds Check__
            if (ply >= (int)Value.MAX_PLY - 10)
                return StaticEvaluation();


            // __Mate distance pruning__
            alpha = Math.Max(-(int)Value.VALUE_MATE + ply, alpha);
            beta = Math.Min((int)Value.VALUE_MATE - ply + 1, beta);
            if (alpha > beta)
                return alpha;
        }
        else
        {
            ///Debug.Assert(alpha != beta); //#DEBUG
            _rootDelta = beta - alpha;
        }


        // __Quiescence search__
        quies = depth <= 0 && !rootNode;


        // __Transposition table lookup__
        /// Note that `_stacks[ply]` hat to be initialized by now.
        /// This will cause errors, if `_stacks` does not get 
        /// initialized before the Search function gets called.
        ///     `_stacks` is an array of value types, thus each elemnt
        ///     get's initialized as soon as the array is initialized.
        ulong ttKey = _board.ZobristKey % (ulong)_TT.Length;
        bool excludedMove = _stacks[StackIndex(ply)].ExcludedMove != Move.NullMove;
        TTEntry tte = _TT[ttKey];
        bool ttHit = tte != default;
        int ttValue = ttHit ? (int)Value.VALUE_NONE : tte.Value;
        Move ttMove = rootNode ? _rootMoves[0] : ttHit ? tte.Move : Move.NullMove;
        bool ttCapture = ttMove.IsCapture;
        _stacks[StackIndex(ply)].TTHit = ttHit;

        // this might require some dummie `Stack`s at the start of the `_stacks` array.
        _stacks[StackIndex(ply)].DoubleExtensions = _stacks[StackIndex(ply) - 1].DoubleExtensions; 
        // `Move.NullMove` calls the parameterless constructor, 
        // which is the default value for a Value-Type (`struct`).
        // Thus, it is the same as if we would let C# handle the
        // initialization of the structs, using their default values, 
        // which is the parameterless constructor, which is essentially
        // `Move.NullMove`.
        /*
        Move bestMove = 
        _stacks[ply + 1].ExcludedMove = 
        _stacks[ply + 2].Killer1 =
        _stacks[ply + 2].Killer2 =
            Move.NullMove;
        */

        if (excludedMove && ttHit)
        {
            _stacks[StackIndex(ply)].TTPv = pvNode || (_stacks[StackIndex(ply)].TTHit && tte.IsPV); //always evaluates to false.

            if (!pvNode
                && tte.Depth > depth
                && ttValue != (int)Value.VALUE_NONE
                && (tte.Bound & (ttValue >= beta ? Bound.BOUND_LOWER : Bound.BOUND_UPPER)) != 0)

                return tte.Value;
        }


        // __[[Static evaluation]] and improvement flag__
        int eval;
        if (_stacks[StackIndex(ply)].InCheck)
        {
            eval = fBase = quies ? -(int)Value.VALUE_INFINITE : (int)Value.VALUE_NONE;
            improving = false;
            goto recursive_search;
        }

        else if (excludedMove && !quies)
            eval = _stacks[StackIndex(ply)].StaticEval;

        else
        {
            if (ttHit)
                eval =
                    tte.Eval == (int)Value.VALUE_NONE
                    ? StaticEvaluation()
                    : tte.Eval;
            else
            {
                eval = StaticEvaluation();
                // Save static evaluation into the transposition table
                _TT[ttKey] = new TTEntry(Move.NullMove, (int)Value.VALUE_NONE, eval, (int)Value.DEPTH_NONE, pvNode, Bound.BOUND_NONE);
            }

            if (quies && eval >= beta)
            {
                _TT[ttKey] = new TTEntry(Move.NullMove, (int)Value.VALUE_NONE, eval, (int)Value.DEPTH_NONE, false, Bound.BOUND_LOWER);

                return eval;
            }

            if (eval > alpha) alpha = eval;

            fBase = eval + 200;
        }

        _stacks[StackIndex(ply)].StaticEval = eval;


        // Again, this requires dummie `Stack`s at the start of the `_stacks` array.
        improving =     _stacks[StackIndex(ply) - 2].StaticEval != (int)Value.VALUE_NONE ? (_stacks[StackIndex(ply)].StaticEval > _stacks[StackIndex(ply) - 2].StaticEval)
                    :   _stacks[StackIndex(ply) - 4].StaticEval != (int)Value.VALUE_NONE ? (_stacks[StackIndex(ply)].StaticEval > _stacks[StackIndex(ply) - 4].StaticEval)
                    : true;


        // __[[Futility Pruning]]__
        if (!_stacks[StackIndex(ply)].TTPv
            && depth < 9
            && eval - ((140 - (cutNode && !ttHit ? 40 : 0)) * depth) >= beta
            && eval >= beta
            && !quies)

            return eval;



        recursive_search:
        // __Loop through all legal moves until no moves remain__
        int bestValue = quies ? eval : -(int)Value.VALUE_INFINITE,
            value = -(int)Value.VALUE_INFINITE,
            moveCount = 0;
        Move bestMove = Move.NullMove;

        foreach (var move in _board.GetLegalMoves(quies))
        {
            if (move == _stacks[StackIndex(ply)].ExcludedMove) 
                continue;

            _stacks[StackIndex(ply)].MoveCount = ++moveCount;

            _board.MakeMove(move);
            bool
                givesCheck = _board.IsInCheck(),
                capture = move.IsCapture,
                fullSearch = false;
            _board.UndoMove(move);

            int newDepth = depth - 1, 
                extension = 0, 
                delta = beta - alpha,
                r = 0,
                d = 0;


            if (!quies)
            {

                r = Reduction(depth) - Reduction(_stacks[StackIndex(ply)].MoveCount);
                r = (r + 1372 - delta * 1073 / _rootDelta) / 1024 + (!improving && r > 936 ? 1 : 0);

                // __Pruning at shallow depth__
                int lmrDepth = newDepth - r;

                if (!rootNode
                  /*&& npm of this color > 0*/)
                {
                    // Skip quiet moves if movecount exceeds our FutilityMoveCount threshold (~8 Elo)

                    if (capture
                        || givesCheck)
                    {

                        // Futility pruning for captures (~2 Elo)

                        // SEE based pruning (~11 Elo)
                    }
                    else
                    {

                        // Continuation history based pruning (~2 Elo)

                        // Futility pruning: parent node (~13 Elo)

                        // Prune moves with negative SEE (~4 Elo)
                    }
                }

                // __Extensions__
                if (ply < _rootDepth * 2)
                {
                    // SES
                    if (!rootNode
                        && depth >= 4
                        && move == ttMove
                        && !excludedMove
                        && ttValue != (int)Value.VALUE_NONE
                        && ((tte.Bound & Bound.BOUND_LOWER) == 0 ? false : true)  // tte.Bound == Bound.BOUND_EXACT || tte.Bound == Bound.BOUND_LOWER
                        && tte.Depth >= depth - 3)
                    {
                        var sBeta = ttValue;
                        var sDepth = (depth - 1) / 2;

                        _stacks[StackIndex(ply)].ExcludedMove = move;
                        value = Search(ply, sBeta - 1, sBeta, sDepth, cutNode, false, false);
                        _stacks[StackIndex(ply)].ExcludedMove = Move.NullMove;

                        if (value < sBeta)
                        {
                            extension = 1;

                            if (!pvNode
                                && value < sBeta - 21
                                && _stacks[StackIndex(ply)].DoubleExtensions <= 11)
                            {
                                extension = 2;
                                depth += depth < 13 ? 1 : 0;
                            }
                        }

                        else if (sBeta >= beta) return sBeta;

                        else if (ttValue >= beta) extension = -2 - (pvNode ? 0 : 1);

                        else if (cutNode) extension = depth > 8 && depth < 17 ? -3 : -1;

                        else if (ttValue <= value) extension = -1;
                    }

                    else if (
                        givesCheck
                        && depth > 9)
                        extension = 1;
                }

                // Update vars after SES
                newDepth += extension;
                _stacks[StackIndex(ply)].DoubleExtensions = _stacks[StackIndex(ply) - 1].DoubleExtensions + (extension == 2 ? 1 : 0);
                _stacks[StackIndex(ply)].CurrentMove = move;
            }


            // __Make the move__
            _board.MakeMove(move);


            // __[[Late Move Reduction]]__
            if (_stacks[StackIndex(ply) + 1].CutoffCount > 8) r++;
            if (cutNode) r += 2;
            if (ttCapture) r++;

            // In quiescend search, we only look at captures. Thus [marked condition]
            // Is always checks for quiescent search aswell and LMR will always be skipped
            // in quiescend search and we don't need to check for it explicitly.
            if (depth >= 2
                && moveCount > 1 + (pvNode && ply <= 1 ? 1 : 0)
                && (!_stacks[StackIndex(ply)].TTPv
                    || !capture) // marked condition
                /*&& !quies*/)
            {
                d = Math.Clamp(newDepth - r, 1, newDepth + 1);
                value = -Search(ply + 1, -(alpha + 1), -alpha, d, true, false, false);

                // Research on a fail high
                if (value > alpha && d < newDepth)
                {
                    int doDeeperSearch = value > (bestValue + 64 + 11 * (newDepth - d)) ? 1 : 0;
                    int doEvenDeeperSearch = value > alpha + 711 && _stacks[StackIndex(ply)].DoubleExtensions <= 6 ? 1 : 0;
                    int doShallowerSearch = value < bestValue + newDepth ? 1 : 0;

                    _stacks[StackIndex(ply)].DoubleExtensions += doEvenDeeperSearch;

                    newDepth += doDeeperSearch - doShallowerSearch + doEvenDeeperSearch;

                    if (newDepth > d)
                        value = -Search(ply + 1, -(alpha + 1), -alpha, newDepth, !cutNode, false, false);
                }
            }

            // __Full-depth search when LMR is skipped__
            else if (!pvNode || moveCount > 1)
            {
                if (ttMove.IsNull && cutNode) r += 2;
                d = newDepth - (r > 3 ? 1 : 0);
                fullSearch = true;
            }

            // __Perform the full depth search__
            // If we dove into quies or we have to research with a full
            // window after LMR was skipped.
            if (quies || fullSearch)
                value = -Search(ply + 1, -beta, -alpha, /*d*/ newDepth, false, false, false);


            // __Undo Move__
            _board.UndoMove(move);


            // __Check for a new best move___
            if (value > bestValue)
            {
                bestMove = move;

                bestValue = value;

                if (value > alpha)
                {
                    if (value >= beta)
                    {
                        _stacks[StackIndex(ply)].CutoffCount += 1 + (ttMove.IsNull ? 0 : 1);

                        // Fail high
                        break;
                    }
                    else
                    {
                        alpha = value;
                    }
                }
            }
        }

        // __Check for mate and stalemate__
        if (moveCount == 0)
            bestValue = _stacks[StackIndex(ply)].InCheck    
                ? -(int)Value.VALUE_INFINITE + ply
                :  (int)Value.VALUE_DRAW;


        // __Make TTEntry and return__
        if ((!excludedMove && !rootNode) || quies)
            _TT[ttKey] = new TTEntry(bestMove, bestValue, eval, depth, pvNode, 
                bestValue >= beta ? Bound.BOUND_LOWER : quies ? Bound.BOUND_UPPER : pvNode && !bestMove.IsNull ? Bound.BOUND_EXACT : Bound.BOUND_UPPER);

        return bestValue;
    }

    int Reduction(int i) => 20 * (int)Math.Log(i);

    /// <summary>
    /// Static evaluation function
    /// </summary>
    /// <returns></returns>
    int StaticEvaluation()
    {
        int mg = 0, eg = 0, phase = 0; 
        foreach (bool white in new[] { true, false })
        {
            for (int piece = 1; piece < 6; piece++)
            {
                ulong mask = _board.GetPieceBitboard((PieceType)piece, white);
                while (mask != 0)
                {
                    phase += piecePhase[piece];
                    var square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (white ? 56 : 0);
                    mg += GetPieceEval(piece - 1, square, 0);
                    eg += GetPieceEval(piece - 1, square, 96);
                }
            }

            mg = -mg;
            eg = -eg;
        }

        return (mg * phase + eg * (24 - phase)) / 24 * (_board.IsWhiteToMove ? 1 : -1);
    }



    int[] pieceVal = { 0, 100, 310, 330, 500, 1000, 10000 };
    int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };
    ulong[] psts = { 657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086, 364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588, 421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452, 162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453, 347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514, 329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460, 257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958, 384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824, 365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484, 329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047, 347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452, 384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716, 366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428, 329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844, 329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863, 419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224, 366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995, 365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612, 401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596, 67159620133902 };


    public int getPstVal(int psq)
    {
        return (int)(((psts[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
    }

    public int StaticEvaluation_DEBUG()
    {
        int mg = 0, eg = 0, phase = 0;

        foreach (bool stm in new[] { true, false })
        {
            for (var p = PieceType.Pawn; p <= PieceType.King; p++)
            {
                int piece = (int)p, ind;
                ulong mask = _board.GetPieceBitboard(p, stm);
                while (mask != 0)
                {
                    phase += piecePhase[piece];
                    ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                    mg += getPstVal(ind) + pieceVal[piece];
                    eg += getPstVal(ind + 64) + pieceVal[piece];
                }
            }

            mg = -mg;
            eg = -eg;
        }

        return (mg * phase + eg * (24 - phase)) / 24 * (_board.IsWhiteToMove ? 1 : -1);
    }
}

// For some reason the `public` modifier
// does not count as a token, so this: 
/*record struct Stack
(
    Move Pv,
    int Ply,
    Move CurrentMove,
    Move ExcludedMove,
    Move Killer1,
    Move Killer2,
    int StaticEval,
    int StatScore,
    int MoveCount,
    bool InCheck,
    bool TtPv,
    bool TtHit,
    int DoubleExtensions,
    int CutoffCnt
);*/
// produces the same amount of tokens. 
// But it is actually 1 token more expensive,
// because of the semicolon at the end.

record struct Stack
{
    public Move Pv;
    public int Ply;
    public Move CurrentMove;
    public Move ExcludedMove;
    public Move Killer1;
    public Move Killer2;
    public int StaticEval;
    public int MoveCount;
    public bool InCheck;
    public bool TTPv;
    public bool TTHit;
    public int DoubleExtensions;
    public int CutoffCount;
}

//record struct TTEntry
//{
//    public Move Move;
//    public int Value;
//    public int Eval;
//    public int Depth;
//    public bool IsPV;
//    public Bound Bound; // can be used as an indication, that the entry is uninitialized
//}

record struct TTEntry
(
    Move Move,
    int Value,
    int Eval,
    int Depth,
    bool IsPV,
    Bound Bound
);