using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum Value : int
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

enum Bound : int
{
    BOUND_NONE,
    BOUND_UPPER,
    BOUND_LOWER,
    BOUND_EXACT = BOUND_UPPER | BOUND_LOWER
};