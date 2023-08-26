using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq; // do not use ```AsParallel()```

public class MyBot : IChessBot
{
    // Constants
    // ===
    int MAX_PLY = 256;
    int PV_COUNT = 100; // high value, search all lines

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
        => (int)(CompressedUSTablesNorm[piece * 8 + square / 8 + mg * 8] >> (square % 8 * 8) & 0xFFul) + UncompressedSPiecesNorm[mg + piece];


    // Fields
    // ===
    Timer _timer;
    Board _board;
    Stack[] _stacks; // indexed by ply
    int[] _rootMoveScores;
    Move[] _rootMoves;
    TTEntry[] _TT = new TTEntry[1<<22]; //Size ~4_000_000


    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        _board = board;
        _stacks = new Stack[MAX_PLY];
        
        _rootMoves = board.GetLegalMoves();

        _rootMoveScores = new int[_rootMoves.Length];

        int alpha,
            beta,
            delta;


        // Iterative Deepening
        for (int depth = 0; depth < MAX_PLY && EnoughTime; ++depth)
        {
            // Init pvCount, pvFirst and pvLast
            int pvCount = Math.Min(PV_COUNT, _rootMoves.Length);

            _rootMoveScores = new int[_rootMoves.Length];

            // Iterate PV lines
            for (int pvIndex = 0; pvIndex < pvCount && EnoughTime; ++pvIndex)
            {
                // Init alpha, beta, delta
                int prev = _rootMoveScores[pvIndex];
                delta = 10 + prev;//delta = 10 + prev * prev / 15799; // TODO: Remove magic numbers, probably dependent on evaluation values
                alpha = prev - delta; ///Math.Max(prev - delta,-(int)Value.VALUE_INFINITE); // cap at infinity?
                beta = prev + delta; ///Math.Min(prev + delta, (int)Value.VALUE_INFINITE); // cap at infinity?

                // Search with increasing asp window
                while (true)
                {
                    _stacks[0].CurrentMove = _rootMoves[pvIndex];
                    _board.MakeMove(_stacks[0].CurrentMove);
                    _rootMoveScores[pvIndex] = -Search(0, alpha, beta, depth, false, true, false, false);
                    _board.UndoMove(_stacks[0].CurrentMove);

                    // TODO
                    // Sort the move
                    // here, only the current move needs to get put into a new position. 
                    // We inverse the scores array, so they get sorted in a descending order

                    if (_rootMoveScores[pvIndex] <= alpha)
                    {
                        beta = (alpha + beta) / 2;
                        alpha = Math.Max(_rootMoveScores[pvIndex] - delta, -(int)Value.VALUE_INFINITE);
                    }
                    else if (_rootMoveScores[pvIndex] >= beta)
                        beta = Math.Min(_rootMoveScores[pvIndex] + delta, (int)Value.VALUE_INFINITE);

                    else break;


                    delta += delta / 3;
                }

                Console.Clear(); //#DEBUG

                Console.WriteLine(_stacks[0].CurrentMove); //#DEBUG
                Console.WriteLine($"Completed Depth: {depth}"); //#DEBUG
                Console.WriteLine("moves searched: " + pvCount); //#DEBUG


                // For some reason, `_rootMoveScores` get's sorted aswell (which is neat)
                Array.Sort(_rootMoveScores, _rootMoves);
                PrintRootMoves(""); //#DEBUG
            }

            // For Debugging purposes
            //for (int i = 0; i < _rootMoves.Length; i++) Console.WriteLine($"{_rootMoves[i]}: {_rootMoveScores[i]}");
        }


        // The best move should now be on the bottom,
        // play the last move in the `_rootMoves` array
        return _rootMoves[^1];
    }

    void PrintRootMoves(string s) //#DEBUG
    { //#DEBUG
        Console.WriteLine(s); //#DEBUG
        for (int i = 0; i < _rootMoves.Length; i++) //#DEBUG
            Console.WriteLine($"{_rootMoves[i]}: {_rootMoveScores[i]}"); //#DEBUG
    } //#DEBUG

    /// <summary>
    /// Recursive search function with quies search.
    /// </summary>
    /// <returns></returns>
    int Search(
        int ply, 
        int alpha, int beta, 
        int depth, 
        bool cutNode, bool rootNode, bool pvNode,
        bool quies)
    {
        // __Repetition__
        if (!rootNode
            && _board.FiftyMoveCounter >= 3
            && alpha < 0/*draw value*/
            && _board.IsRepeatedPosition())
            
            if (0/*alpha*/ >= beta) return 0/*alpha*/;


        // __Check Time___
        if (!EnoughTime) return 100000;


        // __Immediate Draw Evaluation__
        if (!rootNode && _board.IsDraw())
            return 0/*draw value*/;


        // __Mate distance pruning__
        alpha = Math.Max(-(int)Value.VALUE_MATE + ply, alpha);
        beta = Math.Min(-(int)Value.VALUE_MATE + ply + 1, beta);
        if (alpha >= beta)
            return alpha;


        // __Transposition table lookup__
        /// Note that `_stacks[ply]` hat to be initialized by now.
        /// This will cause errors, if `_stacks does not get 
        /// initialized before the Search function gets called.
        var tte = _TT[_board.ZobristKey % (ulong)_TT.Length];
        var ttHit = tte != default;
        var ttValue = ttHit ? (int)Value.VALUE_NONE : tte.Value;
        var ttMove = rootNode ? _rootMoves[0] : ttHit ? tte.Move : Move.NullMove;
        var ttCapture = ttMove.IsCapture;
        _stacks[ply].TTHit = ttHit;

        // this might require some dummies `Stack`s at the start of the `_stacks` array.
        _stacks[ply].DoubleExtensions = _stacks[ply - 1].DoubleExtensions; 
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

        if (_stacks[ply].ExcludedMove != Move.NullMove && ttHit)
        {
            _stacks[ply].TTPv = pvNode || (_stacks[ply].TTHit && tte.IsPV);

            if (!pvNode
                && tte.Depth > depth
                && ttValue != (int)Value.VALUE_NONE
                && (tte.Bound & (ttValue >= beta ? Bound.BOUND_LOWER : Bound.BOUND_UPPER)) != 0)
                
                return tte.Value;
        }


        // 


        return StaticEvaluation();
    }

    /// <summary>
    /// Static evaluation function
    /// </summary>
    /// <returns></returns>
    int StaticEvaluation()
    {
        int mg = 0, 
            eg = 0, 
            phase = 0,
            square;

        foreach (bool white in new[] { true, false })
        {
            for (int piece = 1; piece < 6; piece++)
            {
                ulong mask = _board.GetPieceBitboard((PieceType)piece, white);
                while (mask != 0)
                {
                    phase += PhaseValues[piece];
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (white ? 56 : 0);
                    mg += GetPieceEval(piece - 1, square, 0);
                    eg += GetPieceEval(piece - 1, square, 6);
                }
            }

            mg = -mg;
            eg = -eg;
        }

        return (mg * phase + eg * (5255 - phase)) / 5255 * (_board.IsWhiteToMove ? 1 : -1);
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
    public int StatScore;
    public int MoveCount;
    public bool InCheck;
    public bool TTPv;
    public bool TTHit;
    public int DoubleExtensions;
    public int CutoffCnt;
}

record struct TTEntry
{
    public Move Move;
    public int Value;
    public int Eval;
    public int Depth;
    public bool IsPV;
    public Bound Bound; // can be used as an indication, that the entry is uninitialized
}