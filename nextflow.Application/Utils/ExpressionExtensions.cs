using System.Linq.Expressions;

namespace Nextflow.Application.Utils;

/// <summary>
/// Coleção de métodos de extensão para manipulação dinâmica de <see cref="Expression"/>.
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>
    /// Combina duas expressões Predicate (Func de T para bool) utilizando o operador lógico AND (AndAlso).
    /// Realiza o mapeamento de parâmetros para garantir que ambas usem a mesma referência de entrada.
    /// </summary>
    /// <typeparam name="T">O tipo da entidade.</typeparam>
    /// <param name="left">A expressão base (esquerda).</param>
    /// <param name="right">A nova expressão a ser anexada (direita).</param>
    /// <returns>Uma nova expressão composta.</returns>
    public static Expression<Func<T, bool>> AndAlso<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var param = Expression.Parameter(typeof(T), "e");

        var leftBody = new ParameterReplaceVisitor(left.Parameters[0], param).Visit(left.Body)!;
        var rightBody = new ParameterReplaceVisitor(right.Parameters[0], param).Visit(right.Body)!;

        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), param);
    }

    private sealed class ParameterReplaceVisitor(Expression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
