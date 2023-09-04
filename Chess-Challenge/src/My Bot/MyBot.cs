﻿using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics; // USED ONLY FOR `Debugger.IsAttached`

public class MyBot : IChessBot
{
    // Initialization
    // ==============
    // 
    public MyBot() 
    {
        int ctr=0;
        // Big table packed with data from premade piece square tables
        // Access using using PackedEvaluationTables[square][pieceType] = score
        UnpackedPestoTables = new[] {
            63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m, 75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m, 936945638387574698250991104m, 75531285965747665584902616832m,
            77047302762000299964198997571m, 3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m, 3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m, 2475894077091727551177487608m,
            2458978764687427073924784380m, 3718684080556872886692423941m, 4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m, 9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
            77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m, 5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m, 5619082524459738931006868492m, 649197923531967450704711664m,
            75809334407291469990832437230m, 78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m, 5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m, 76772801025035254361275759599m,
            75502243563200070682362835182m, 78896921543467230670583692029m, 2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m, 3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
            73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m, 3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m, 78580145051212187267589731866m, 75798434925965430405537592305m,
            68369566912511282590874449920m, 72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m, 73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m, 70529879645288096380279255040m,
        }.Select(packedTable =>
        new BigInteger(packedTable).ToByteArray().Take(12)
                    // Using search max time since it's an integer than initializes to zero and is assgined before being used again 
                    .Select(square => (int)((sbyte)square * 1.461) + PieceValues[ctr++ % 12])
                .ToArray()
        ).ToArray();
    }


    // Constants
    // =========
    int STACK_DUMMIES = 6;

    // Pawn, Knight, Bishop, Rook, Queen, King 
    short[] PieceValues = { 82, 337, 365, 477, 1025, 0,     // Middlegame
                            94, 281, 297, 512, 936, 0 };    // Endgame

    int[][] UnpackedPestoTables;



    // Getters/Setters
    // ===============
    int StackIndex(int index) => index + STACK_DUMMIES;
    //int Alpha(int value) => Math.Max(value,-(int)Value.VALUE_INFINITE);
    //int Beta (int value) => Math.Min(value, (int)Value.VALUE_INFINITE);
    // [Used_time] > [Total_time] / [Avarage_moves_in_a_game]
    bool EnoughTime => _timer.MillisecondsElapsedThisTurn <= _timer.MillisecondsRemaining / 30;

    //int Reduction(int i) => 20 * (int)Math.Log(i);



    // Fields
    // =====
    Timer _timer;
    Board _board;

    // indexed by ply
    Stack[] _stacks; 
    // Transposition Table
    // Size: 1<<22 = 0x400000
    TTEntry[] _TT = new TTEntry[0x400000];
    // the current depth in the iterative deepening loop
    int _rootDepth;



    // Search function called by API
    // =============================
    public Move Think(Board board, Timer timer)
    {
        Console.WriteLine("MyBot"); //#DEBUG


        _timer = timer;
        _board = board;
        _stacks = new Stack[(int)Value.MAX_PLY + STACK_DUMMIES];


        // Iterative Deepening
        _rootDepth = 0;
        while ( ++_rootDepth < (int)Value.MAX_PLY
                && EnoughTime)
        {
            Search(-1, -(int)Value.VALUE_INFINITE, (int)Value.VALUE_INFINITE, _rootDepth, false, true);

            Console.WriteLine($"Depth: {_rootDepth}");   //#DEBUG
        }
        
        
        // The best move should now be on the bottom,
        // play the last move in the `_rootMoves` array
        return _stacks[StackIndex(0)].Pv;
    }



    // Helper functions
    // ================
    string StringPV => "PV " + new string(CharPV); //#DEBUG
    char[] CharPV => PvStrings().SelectMany(x => x.ToCharArray()).ToArray(); //DEBUG
    string[] PvStrings()
    {
        string[] moves = PV.Select(x => x.IsNull ? "" : ExtractMove(x)).ToArray(); //#DEBUG
        string[] evals = PvEvals.Select(x => x.ToString() + "").ToArray(); //#DEBUG
        string[] staticEvals = StaticPvEvals.Select(x => x.ToString() + "").ToArray(); //#DEBUG

        string[] result = new string[moves.Length];
        for (int i = 0; i < moves.Length; i++)
            result[i] = moves[i] == "" ? "" : moves[i] + "("+evals[i]+"|" + staticEvals[i] + ")";

        return result;
    }

    Move[] PV => _stacks.Select(x => x.Pv).ToArray(); //#DEBUG
    int[] PvEvals => _stacks.Select(x => x.Eval).ToArray(); //#DEBUG
    int[] StaticPvEvals => _stacks.Select(x => x.StaticPvEval).ToArray(); //#DEBUG

    string ExtractMove(Move x) => x.ToString().Replace("Move", "").Replace(":", ""); //#DEBUG



    // Recursive search function
    // =========================
    /// <summary>
    /// Recursive search function with quies search.
    /// </summary>
    /// <returns></returns>
    int Search(int ply, int alpha, int beta, int depth, bool cutNode, bool pvNode)
    {
        // __Quiescence search__
        ref Stack stack = ref _stacks[StackIndex(++ply)];
        ref TTEntry tte = ref _TT[_board.ZobristKey % 0x400000];
        bool root = ply == 0, quies = --depth <= 0, ttHit = tte != default;
        int bestEval = -(int)Value.VALUE_INFINITE, eval, parentAlpha = alpha;
        Move bestMove = default; //`default` is the same as the paramterless contsr., which is the same as Move.NullMove


        if (!root)
        {
            // __Repetition__
            if (_board.IsRepeatedPosition())
                return (int)Value.VALUE_DRAW;


            // __Bounds Check__
            // We can assume that we will never reach a ply of 246.
            //if (ply == (int)Value.MAX_PLY
            //    || depth <= 0) // this will be replaced by qsearch 
            //    return staticEval;



            // __Mate distance pruning__

        }


        // __Transposition table lookup__
        // this can never be true in the root, because there are no tte made 
        // at the root depth
        stack.TTPv = pvNode || tte.IsPV;
        if (!pvNode
            && tte.Depth > depth
            && tte.Eval != (int)Value.VALUE_NONE
            && (tte.Bound & (tte.Eval >= beta ? Bound.BOUND_LOWER : Bound.BOUND_UPPER)) != 0)
        {
            return tte.Eval;
        }


        // __[[Static evaluation]] and improvement flag__
        stack.StaticEval = eval = Evaluate();



        // __[[Futility Pruning]]__



        // __Sorting__
        var moves = _board.GetLegalMoves(quies);
        var scores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            scores[i] = -(
                // tt pv
                move == tte.Pv ? 20000 :

                // stack pv
                move == stack.Pv ? 15000 :

                // killers
                move == stack.Killer ? 10000 :

                // mvv lva
                move.IsCapture ? 50 * (int)move.CapturePieceType - (int)move.MovePieceType 
                
                : 0
            );
        }
        Array.Sort(scores, moves);


        // __Quiescent search__
        if (quies)
        {
            bestEval = eval;

            if (bestEval >= beta
                || moves.Length == 0)
                return bestEval;

            if (bestEval > alpha) 
                alpha = bestEval;
        }


        // __Loop through all legal moves__
        for(int i = 0; i < moves.Length; i++)
        {
            if (!quies)
            {
                // __Pruning at shallow depth__

                
                // __Extensions__

            }


            // __Make the move__
            _board.MakeMove(moves[i]);


            // __[[Late Move Reduction]]__
            // Research on a fail high; move could be good


            // __Full-depth search when LMR is skipped__


            // __Perform the full depth search__
            // If we dove into quies or we have to research with a full
            // window after LMR was skipped.
            eval = -Search(ply, -beta, -alpha, depth, !cutNode, i == 0);


            // __Undo Move__
            _board.UndoMove(moves[i]);

            //if (root) Console.WriteLine($"{moves[i]}: {eval}");

            // __Check for a new best move___
            if (eval > bestEval) 
            {
                bestEval = eval;

                if (eval > alpha)
                {
                    alpha = eval;
                    bestMove = moves[i];

                    if (eval >= beta)
                    {
                        stack.CutoffCount++;
                        stack.Killer = moves[i];
                        break;
                    }
                }
            }

            if (!EnoughTime) return (int)Value.VALUE_INFINITE;
        }


        // __Check for mate and stalemate__
        if (!quies && moves.Length == 0)
            return _board.IsInCheck() ? ply - (int)Value.VALUE_MATE : (int)Value.VALUE_DRAW;
        


        // __Make TTEntry__
        Bound bound = bestEval >= beta ? Bound.BOUND_LOWER : bestEval > parentAlpha ? Bound.BOUND_EXACT : Bound.BOUND_UPPER;
        if (!root) tte = new TTEntry(bestMove, bestEval, stack.StaticEval, depth, pvNode, bound);

        if (root || pvNode)
        {
            stack.Eval = bestEval;
            stack.Pv = bestMove;


            if (Debugger.IsAttached) //#DEBUG
            { //#DEBUG

                stack.StaticPvEval = Evaluate(); //#DEBUG

            } //#DEBUG
        }

        if (root && Debugger.IsAttached)  //#DEBUG
        { //#DEBUG


            Console.WriteLine(StringPV); //#DEBUG


        } //#DEBUG


        return bestEval;
    }




    // Static Evaluation
    // =================
    /// <summary>
    /// Static evaluation function
    /// From https://github.com/Tyrant7/Chess-Challenge/blob/main/Chess-Challenge/src/My%20Bot/MyBot.cs
    /// </summary>
    /// <returns></returns>
    int Evaluate()
    {
        int middlegame = 0, endgame = 0, gamephase = 0, sideToMove = 2, piece, square;
        for (; --sideToMove >= 0; middlegame = -middlegame, endgame = -endgame)
            for (piece = -1; ++piece < 6;)
                for (ulong mask = _board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;)
                {
                    // Gamephase, middlegame -> endgame
                    // Multiply, then shift, then mask out 4 bits for value (0-16)
                    gamephase += 0x00042110 >> piece * 4 & 0x0F;

                    // Material and square evaluation
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;
                    middlegame += UnpackedPestoTables[square][piece];
                    endgame += UnpackedPestoTables[square][piece + 6];
                }

        // Tempo bonus to help with aspiration windows
        return (middlegame * gamephase + endgame * (24 - gamephase)) / 24 * (_board.IsWhiteToMove ? 1 : -1) + gamephase / 2;
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
    public Move CurrentMove;
    public Move ExcludedMove;
    public Move Killer;
    public int StaticEval;
    public int Eval;
    public int MoveCount;
    public bool InCheck;
    public bool TTPv;
    public bool TTHit;
    public int DoubleExtensions;
    public int CutoffCount;

    public int StaticPvEval; //#DEBUG
}
record struct TTEntry
(
    Move Pv,
    int Eval,
    int StaticEval,
    int Depth,
    bool IsPV,
    Bound Bound // Bound=Bound.BOUND_NONE can be used as an indication, that the entry is uninitialized (?)
);