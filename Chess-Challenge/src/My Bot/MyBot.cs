using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq; // do not use ```AsParallel()```
using System.ComponentModel;
using ChessChallenge.Application;
using System.Runtime.InteropServices;

public class MyBot : IChessBot
{
    // Constants
    // ===
    int MAX_PLY = 256;
    int PV_COUNT = 100; // high value, search all lines

    // Getters/Setters
    // ===
    // [Used_time] > [Total_time] / [Avarage_moves_in_a_game]
    bool EnoughTime => _timer.MillisecondsElapsedThisTurn <= _timer.MillisecondsRemaining / 30;

    // Fields
    // ===
    Timer _timer;
    Board _board;
    Stack[] _stacks;
    int[] _rootMoveScores;
    Move[] _rootMoves;


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
                delta = 10 + prev * prev / 15799; // TODO: Remove magic numbers, probably dependent on evaluation values
                alpha = prev - delta; ///Math.Max(prev - delta,-(int)Value.VALUE_INFINITE); // cap at infinity?
                beta = prev + delta; ///Math.Min(prev + delta, (int)Value.VALUE_INFINITE); // cap at infinity?

                // Search with increasing asp window
                while (true)
                {
                    _stacks[0].CurrentMove = _rootMoves[pvIndex];
                    _rootMoveScores[pvIndex] = Search(0, alpha, beta, depth, false, true, false, _stacks[0]);

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

                Console.Clear();

                Console.WriteLine(_stacks[0].CurrentMove);
                Console.WriteLine($"Completed Depth: {depth}");
                Console.WriteLine("moves searched: " + pvCount);


                // For some reason, `_rootMoveScores` get's sorted aswell,
                // which is handy 
                PrintRootMoves("\nUnsorted"); 
                Array.Sort(_rootMoveScores, _rootMoves);
                PrintRootMoves("Sorted");
            }

            // For Debugging purposes
            //for (int i = 0; i < _rootMoves.Length; i++) Console.WriteLine($"{_rootMoves[i]}: {_rootMoveScores[i]}");
        }


        // The best move should now be on the bottom,
        // play the last move in the `_rootMoves` array
        return _rootMoves[^1];
    }

    void PrintRootMoves(string s)
    {
        Console.WriteLine(s);
        for (int i = 0; i < _rootMoves.Length; i++)
        {
            Console.WriteLine($"{_rootMoves[i]}: {_rootMoveScores[i]}");
        }
    }

    /// <summary>
    /// Recursive search function.
    /// </summary>
    /// <returns></returns>
    int Search(
        int ply, 
        int alpha, int beta, 
        int depth, 
        bool cutNode, bool rootNode, 
        bool quies,
        Stack s)
    {
        // Simulate some searching 
        System.Threading.Thread.Sleep(depth * 5);

        return StaticEvaluation(s);
    }

    /// <summary>
    /// Static evaluation function
    /// </summary>
    /// <returns></returns>
    int StaticEvaluation(Stack s)
    {
        // if the move has already been assigned a random eval, 
        // return that
        if (_rootMoveScores[Array.IndexOf(_rootMoves, s.CurrentMove)] != 0)
            return _rootMoveScores[Array.IndexOf(_rootMoves, s.CurrentMove)];

        // else
        // return a new new random value as eval
        return new Random().Next(10000) + 1; // cannot generate 0

        // (For this we can also use the TT, when implemented)
    }
}

record struct Stack
(
    Move Pv,
    int Ply,
    Move CurrentMove,
    Move ExcludedMove,
    Move Killer1,
    Move Killer2,
    Value StaticEval,
    int StatScore,
    int MoveCount,
    bool InCheck,
    bool TtPv,
    bool TtHit,
    int DoubleExtensions,
    int CutoffCnt
);

enum Bound
{
    BOUND_NONE,
    BOUND_UPPER,
    BOUND_LOWER,
    BOUND_EXACT = BOUND_UPPER | BOUND_LOWER
};
enum Value : int
{
    MAX_PLY = 246,
    VALUE_ZERO = 0,
    VALUE_DRAW = 0,
    VALUE_KNOWN_WIN = 10000,
    VALUE_MATE = 32000,
    VALUE_INFINITE = 32001,
    VALUE_NONE = 32002,
    VALUE_TB_WIN_IN_MAX_PLY = VALUE_MATE - 2 * MAX_PLY,
    VALUE_TB_LOSS_IN_MAX_PLY = -VALUE_TB_WIN_IN_MAX_PLY,
    VALUE_MATE_IN_MAX_PLY = VALUE_MATE - MAX_PLY,
    VALUE_MATED_IN_MAX_PLY = -VALUE_MATE_IN_MAX_PLY,

    // In the code, we make the assumption that these values
    // are such that non_pawn_material() can be used to uniquely
    // identify the material on the board.
    PawnValueMg = 126, PawnValueEg = 208,
    KnightValueMg = 781, KnightValueEg = 854,
    BishopValueMg = 825, BishopValueEg = 915,
    RookValueMg = 1276, RookValueEg = 1380,
    QueenValueMg = 2538, QueenValueEg = 2682,
    MidgameLimit = 15258, EndgameLimit = 3915
};

record struct TTEntry
(
    Move Move,
    Value Value,
    Value Eval,
    int Depth,
    bool IsPV,
    Bound Bound
);
