using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq; // do not use ```AsParallel()```
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;
using System.Diagnostics;

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
    int[] UncompressedSPiecesNorm =
    {
        // mg
        215,  429,  393,  547,  979,   71,

        // eg
        286,  302,  309,  511,  984,   28
    };
    ulong[] CompressedUSTablesNorm = 
    {
        // mg
        10199964370168810893, 8551772634709032921, 8257274873413599611, 7533543106725840492, 6954542469576487776, 7750266653533630304, 7322444449926839645, 10199964370168810893,
        1326636256005863168, 7818173725130639924, 12595160614255113557, 11576167150659346303, 9412417275444106621, 8762478317095844978, 8609639521908119143, 8032292511186973987,
        13459180807964182939, 11443357342921838003, 16355103418390212813, 14756312365290736067, 14758526794291796414, 14976123353450337734, 14188266063050430916, 12728795175876152492,
        14754567397000404170, 15690820377509810122, 13686690784333580206, 11867222245484637597, 11803852957549699223, 11654118174050331286, 9567002134121067928, 12077455448336477352,
        14831164951255036814, 15693044302226951843, 17578393589174022835, 13886795229915161246, 13600228440659763640, 14035958301778362796, 13239678834149929111, 9771588189855133879,
        14974373742790115246, 11145777725566013918, 13248712439050261466, 9055229932610433209, 8904038024931625376, 11502894294299359168, 14612102170287065285, 15410087302912206259,

        // eg
        5063812098665367110, 18440775730378764535, 10923050426825222048, 5787202758781065058, 4413023200430737229, 3762534537959522882, 3907782274636268877, 5063812098665367110,
        10281097634892463799, 13247009429336483022, 14256678884106099407, 15776100689995754205, 15559646420019962841, 15120524526790174420, 13823474592504665539, 12230886910609112015,
        16207027828119233003, 16497227442415006188, 17362493615518248175, 17650726233861585392, 17000799378815578348, 16424049489229902312, 15773837869144008680, 16784330810674769888,
        17940930216121464315, 17797085452071074810, 17433980571712026873, 18083634722155526136, 17361931791048310522, 16930425128405563637, 17648749268971286769, 16281338312926687981,
        15475712811272229310, 13315118792881920435, 13530169991798112680, 16068251932846971078, 15051563084446883237, 14181508460256797879, 11573870266372827827, 10930978176345743262,
        15629160701053745562, 17073989426577205970, 16717358436306578145, 16280222325921672404, 15412993567579297483, 15629446720862935753, 14905483150697747396, 13098962305856421037
    };
    //int[] UncompressedSMobilityValues =
    //{
    //     //                 opponent                            friendly                  
    //     // -     P     N     B     R     Q     K      P      N     B     R     Q     K   
    //        0,    0,   28,   36,   17,   31,   89,     10,    9,   11,    3,   -1,   -5,  // P
    //        1,   -4,    0,   20,   19,   18,   34,      1,    3,    0,    2,    4,    3,  // N
    //        2,   -1,   22,    0,   15,   30,   67,     -8,    3,   54,    4,   -1,   -3,  // B
    //        2,   -1,    6,   15,    0,   31,   36,    -11,    3,   -2,    4,    2,   -1,  // R
    //        3,   -3,   -3,    4,    1,    0,   75,     -3,    5,    6,    1,  -99,   -4,  // Q
    //        0,   30,    3,   12,    5,  -99,    0,      6,    4,    7,   -8,    6,    0,  // K
    //};


    // Getters/Setters
    // ===

    // [Used_time] > [Total_time] / [Avarage_moves_in_a_game]
    bool EnoughTime => _timer.MillisecondsElapsedThisTurn <= _timer.MillisecondsRemaining / 30;

    int GetPieceEval(int piece, int square, int mg) 
        => (int) (CompressedUSTablesNorm[piece * 8 + square / 8 + mg * 8] >> (square % 8 * 8) & 0xFFul) 
                + UncompressedSPiecesNorm[mg + piece];

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
    static TTEntry[] _TT = new TTEntry[1<<22]; //Size ~4_000_000
    int _rootDelta, _rootDepth, _currentRootMove, _bestValue;
    Move _rootBestMove;


    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _board = board;
        _stacks = new Stack[(int)Value.MAX_PLY + STACK_DUMMIES];
        
        _rootMoves = board.GetLegalMoves();

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

                // Search with increasing asp window
                int failHighs = 0;
                while (true)
                {
                    _stacks[StackIndex(0)].CurrentMove = _rootMoves[_currentRootMove];

                    _board.MakeMove(_rootMoves[_currentRootMove]);
                    int newEval = -Search(0, alpha, beta, _rootDepth - failHighs * 2, false, true, false);
                    _board.UndoMove(_rootMoves[_currentRootMove]);

                    _rootMoveScoresAVG[_currentRootMove] = _rootDepth == 0 ? newEval : (newEval * 2 + _rootMoveScoresAVG[_currentRootMove]) / 3;


                    if (newEval >= (int)Value.VALUE_MATE_IN_MAX_PLY)
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
            Array.Sort(_rootMoveScoresAVG, _rootMoves);

            if (EnoughTime) { 
                _rootBestMove = _rootMoves[^1];

                PrintRootMoves("\n\n"); //#DEBUG
                Console.WriteLine($"Completed Depth: {_rootDepth}"); //#DEBUG
            }

            if (!EnoughTime) break;
        }


        //PrintRootMoves("\n\n"); //#DEBUG
        //Console.WriteLine($"Completed Depth: {_rootDepth}"); //#DEBUG


        // The best move should now be on the bottom,
        // play the last move in the `_rootMoves` array
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
            Debug.Assert(alpha != beta);

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
        if (_board.IsInCheck())
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


        //// __[[Futility Pruning]]__
        //if (!_stacks[StackIndex(ply)].TTPv
        //    && depth < 9
        //    && eval - ((140 - (cutNode && !ttHit ? 40 : 0)) * depth) >= beta
        //    && eval >= beta
        //    && !quies)

        //    return eval;



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


            //if (!quies)
            //{
            
            //    r = Reduction(depth) - Reduction(_stacks[StackIndex(ply)].MoveCount);
            //    r = (r + 1372 - delta * 1073 / _rootDelta) / 1024 + (!improving && r  > 936 ? 1 : 0);

            //    // __Pruning at shallow depth__
            //    int lmrDepth = newDepth - r;

            //    if (!rootNode 
            //      /*&& npm of this color > 0*/)
            //    {
            //        // Skip quiet moves if movecount exceeds our FutilityMoveCount threshold (~8 Elo)

            //        if (capture
            //            || givesCheck)
            //        {

            //            // Futility pruning for captures (~2 Elo)

            //            // SEE based pruning (~11 Elo)
            //        }
            //        else
            //        {

            //            // Continuation history based pruning (~2 Elo)

            //            // Futility pruning: parent node (~13 Elo)

            //            // Prune moves with negative SEE (~4 Elo)
            //        }
            //    }

            //    // __Extensions__
            //    if (ply < _rootDepth*2)
            //    {
            //        // SES
            //        if (!rootNode
            //            && depth >= 4
            //            && move == ttMove
            //            && !excludedMove
            //            && ttValue != (int)Value.VALUE_NONE
            //            && ((tte.Bound & Bound.BOUND_LOWER) == 0 ? false : true)  // tte.Bound == Bound.BOUND_EXACT || tte.Bound == Bound.BOUND_LOWER
            //            && tte.Depth >= depth - 3)
            //        {
            //            var sBeta = ttValue;
            //            var sDepth = (depth - 1) / 2;

            //            _stacks[StackIndex(ply)].ExcludedMove = move;
            //            value = Search(ply, sBeta - 1, sBeta, sDepth, cutNode, false, false);
            //            _stacks[StackIndex(ply)].ExcludedMove = Move.NullMove;

            //            if (value < sBeta)
            //            {
            //                extension = 1;

            //                if (!pvNode
            //                    && value < sBeta - 21
            //                    && _stacks[StackIndex(ply)].DoubleExtensions <= 11)
            //                {
            //                    extension = 2;
            //                    depth += depth < 13 ? 1 : 0;
            //                }
            //            }

            //            else if (sBeta >= beta) return sBeta;

            //            else if (ttValue >= beta) extension = -2 - (pvNode ? 0 : 1);

            //            else if (cutNode) extension = depth > 8 && depth < 17 ? -3 : -1;

            //            else if (ttValue <= value) extension = -1;
            //        }

            //        else if (
            //            givesCheck
            //            &&  depth > 9)
            //            extension = 1;
            //    }

            //    // Update vars after SES
            //    newDepth += extension;
            //    _stacks[StackIndex(ply)].DoubleExtensions = _stacks[StackIndex(ply) - 1].DoubleExtensions + (extension == 2 ? 1 : 0);
            //    _stacks[StackIndex(ply)].CurrentMove = move;
            //}

            
            // __Make the move__
            _board.MakeMove(move);


            // __[[Late Move Reduction]]__
            //if (_stacks[StackIndex(ply) + 1].CutoffCount > 8) r++;
            //if (cutNode) r += 2;
            //if (ttCapture) r++;            

            //// In quiescend search, we only look at captures. Thus [marked condition]
            //// Is always checks for quiescent search aswell and LMR will always be skipped
            //// in quiescend search and we don't need to check for it explicitly.
            //if (depth >= 2
            //    && moveCount > 1 + (pvNode && ply <= 1 ? 1 : 0)
            //    && (!_stacks[StackIndex(ply)].TTPv
            //        || !capture ) // marked condition
            //    /*&& !quies*/) 
            //{
            //    d = Math.Clamp(newDepth - r, 1, newDepth + 1);
            //    value = -Search(ply + 1, -(alpha + 1), -alpha, d, true, false, false);

            //    // Research on a fail high
            //    if (value > alpha && d < newDepth)
            //    {
            //        int doDeeperSearch = value > (bestValue + 64 + 11 * (newDepth - d)) ? 1 : 0;
            //        int doEvenDeeperSearch = value > alpha + 711 && _stacks[StackIndex(ply)].DoubleExtensions <= 6 ? 1 : 0;
            //        int doShallowerSearch = value < bestValue + newDepth ? 1 : 0;

            //        _stacks[StackIndex(ply)].DoubleExtensions += doEvenDeeperSearch;

            //        newDepth += doDeeperSearch - doShallowerSearch + doEvenDeeperSearch;

            //        if (newDepth > d)
            //            value = -Search(ply + 1, -(alpha + 1), -alpha, newDepth, !cutNode, false, false);
            //    }
            //}

            //// __Full-depth search when LMR is skipped__
            //else if (!pvNode || moveCount > 1)
            //{
            //    if (ttMove.IsNull && cutNode) r += 2;
            //    d = newDepth - (r > 3 ? 1 : 0);
            //    fullSearch = true;
            //}

            // __Perform the full depth search__
            // If we dove into quies or we have to research with a full
            // window after LMR was skipped.
            //if (quies || fullSearch) 
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
        int mg = 0,
            eg = 0,
            phase = 0;

        foreach (bool white in new[] { true, false })
        {
            for (int piece = 1; piece < 6; piece++)
            {
                ulong mask = _board.GetPieceBitboard((PieceType)piece, white);
                while (mask != 0)
                {
                    phase += PhaseValues[piece];
                    var square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (white ? 56 : 0);
                    mg += GetPieceEval(piece - 1, square, 0);
                    eg += GetPieceEval(piece - 1, square, 6);
                }
            }

            mg = -mg;
            eg = -eg;
        }

        return (int)(
            (mg * phase + eg * (5255 - phase)) / 5255 * (_board.IsWhiteToMove ? 2 : -2)
        );
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