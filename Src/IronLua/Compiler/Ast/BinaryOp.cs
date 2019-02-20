namespace IronLua.Compiler.Ast
{
    enum BinaryOp
    {
        Or,
        And,
        Equal,
        NotEqual,
        Less,
        Greater,
        LessEqual,
        GreaterEqual,
        Concat,
        Add,
        Subtract,
        Multiply,
        Divide, //  float division
        IntDivide, // floor division
        Mod,
        Power,
        BitwiseAnd,
        BitwiseOr,
        BitwiseXor,
        BitwiseLeftShift,
        BitwiseRightShift
    }
}
