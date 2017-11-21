using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ExpressionSerialization
{
    /// <summary>
    ///     Explicit interface implementation of IQueryable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Query<T> : IQueryable<T>, IQueryable, IEnumerable<T>
        , IEnumerable, IOrderedQueryable<T>, IOrderedQueryable // where T : new()
    {
        private readonly Expression expression;

        //QueryProvider provider;
        private readonly IQueryProvider provider;


        //public Query(QueryProvider provider)
        public Query()
            : this(new T[1].AsQueryable().Provider)
        {
        }

        public Query(IQueryProvider provider)
        {
            this.provider = provider;
            expression =
                Expression.Constant(this); //this function implicitly calls the ToString method in Debug
        }

        public Query(IQueryProvider provider, Expression expression)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (expression == null)
                throw new ArgumentNullException("expression");
            if (!(typeof(IQueryable<T>).IsAssignableFrom(expression.Type) ||
                  typeof(IEnumerable<T>).IsAssignableFrom(expression.Type)))
                throw new ArgumentOutOfRangeException("expression");
            this.provider = provider;
            this.expression = expression;
        }

        Expression IQueryable.Expression => expression;

        Type IQueryable.ElementType => typeof(T);

        IQueryProvider IQueryable.Provider => provider;

        /// <summary>
        ///     on the call to any of the System.Linq extension methods on IEnumerable{T}, this
        ///     method will get called. <see cref="http://msdn.microsoft.com/en-us/library/system.linq.enumerable.aspx" />
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>) provider.Execute(expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) provider.Execute(expression)).GetEnumerator();
        }

        /// <summary>
        ///     in Debug, this is called implicitly.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return GetType().FullName; // this.provider.GetQueryText(this.expression);
        }
    }
}