using Google.OrTools.LinearSolver;
using System;
using Xunit;

namespace OptimizerTests
{
#nullable disable

    public class LinearExprTests
    {
        [Fact]
        static void Given_a_LinearExpr_and_a_solution_When_SolutionValue_is_called_then_the_result_is_correct()
        {
            Console.WriteLine("Given_a_LinearExpr_and_a_solution_When_SolutionValue_is_called_then_the_result_is_correct");
            Solver solver = Solver.CreateSolver("glop");
            // x, y and z are fixed; we don't want to test the solver here.
            Variable x = solver.MakeIntVar(3, 3, "x");
            Variable y = solver.MakeIntVar(4, 4, "y");
            Variable z = solver.MakeIntVar(5, 5, "z");

            LinearExpr objective = x * 2 + y * 3 + z + 4; // 27
            LinearExpr anew = new();
            solver.Maximize(objective);
            solver.Solve();
            Assert.Equal(27, objective.SolutionValue(), precision: 9);
            Assert.Equal(0, anew.SolutionValue(), precision: 9);
            Assert.Equal(27, (objective + anew).SolutionValue(), precision: 9);
        }
    }
}