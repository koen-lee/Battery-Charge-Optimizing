using Google.OrTools.LinearSolver;
using System.Reflection;
#nullable disable

public static class LinearExprExtensions
{
    /// <summary>
    /// Yuck.
    /// Only exists because it is not implemented in the library.
    /// </summary>
    /// <param name="expr">The expression to evaluate</param>
    /// <returns>The calculated result, using SolutionValue() for variables.</returns>
    /// <exception cref="NotImplementedException">in case the private parts of the lib change</exception>
    public static double SolutionValue(this LinearExpr expr)
    {
        switch (expr.GetType().Name)
        {
            case "LinearExpr":
                return 0;
            case "Sum":
                return EvaluateSum(expr);
            case "ProductCst":
                return EvaluateProductCst(expr);
            case "SumCst":
                return EvaluateSumCst(expr);
            case "VarWrapper":
                return EvaluateVarWrapper(expr);
            default:
                throw new NotImplementedException(expr.GetType().Name);
        }
    }

    private static double EvaluateSum(LinearExpr expr)
    {
        if (Sum_left_ == null) Sum_left_ = expr.GetType().GetField("left_", privateField); // private class, so no 
        if (Sum_right_ == null) Sum_right_ = expr.GetType().GetField("right_", privateField);
        var left = (LinearExpr)Sum_left_.GetValue(expr);
        var right = (LinearExpr)Sum_right_.GetValue(expr);
        return SolutionValue(left) + SolutionValue(right);
    }

    private static double EvaluateProductCst(LinearExpr expr)
    {

        if (ProductCst_coeff_ == null) ProductCst_coeff_ = expr.GetType().GetField("coeff_", privateField);
        if (ProductCst_expr_ == null) ProductCst_expr_ = expr.GetType().GetField("expr_", privateField);
        var coeff_ = (double)ProductCst_coeff_.GetValue(expr);
        var expr_ = (LinearExpr)ProductCst_expr_.GetValue(expr);
        return coeff_ * SolutionValue(expr_);
    }

    private static double EvaluateSumCst(LinearExpr expr)
    {
        if (SumCst_coeff_ == null) SumCst_coeff_ = expr.GetType().GetField("coeff_", privateField);
        if (SumCst_expr_ == null) SumCst_expr_ = expr.GetType().GetField("expr_", privateField);
        var coeff_ = (double)SumCst_coeff_.GetValue(expr);
        var expr_ = (LinearExpr)SumCst_expr_.GetValue(expr);
        return coeff_ + SolutionValue(expr_);
    }

    private static double EvaluateVarWrapper(LinearExpr expr)
    {
        if (VarWrapper_var_ == null) VarWrapper_var_ = expr.GetType().GetField("var_", privateField);
        Variable var = (Variable)VarWrapper_var_.GetValue(expr);
        return var.SolutionValue();
    }

    static BindingFlags privateField = BindingFlags.Instance | BindingFlags.NonPublic;
    private static FieldInfo VarWrapper_var_;
    private static FieldInfo SumCst_expr_;
    private static FieldInfo SumCst_coeff_;
    private static FieldInfo ProductCst_expr_;
    private static FieldInfo ProductCst_coeff_;
    private static FieldInfo Sum_left_;
    private static FieldInfo Sum_right_;
}
