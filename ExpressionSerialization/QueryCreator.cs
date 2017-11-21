using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ExpressionSerialization
{
    /// <summary>
    ///     Creates (IQueryable) Query instances that actually have a backing data source.
    ///     this class is almost analagous to the DLinqSerializer class in that it works with
    ///     the CustomExpressionXmlConverter by providing the data source to the Query (IQueryable)
    ///     when it is deserialized.
    /// </summary>
    public class QueryCreator
    {
        private readonly Func<Type, dynamic> fnGetObjects;

        public QueryCreator()
            : this(GetIEnumerableOf)
        {
        }

        /// <summary>
        ///     Relies upon a function to get objects. Could alternatively have required
        ///     an interface as a ctor argument, but a fn. parameters seems even more generic an approach.
        ///     If we only need 1 method, then why require an entire interface? Also, this way allows a static class method
        ///     to be used.
        /// </summary>
        /// <param name="fngetobjects">
        ///     function that returns a dynamic
        ///     which is the IEnumerable`1 of element type that is the Type argument to the function (fngetobjects).
        /// </param>
        public QueryCreator(Func<Type, dynamic> fngetobjects)
        {
            fnGetObjects = fngetobjects;
        }
        //Func<Type, IEnumerable<object>> fnGetObjects;

        #region CreateQuery

        /// <summary>
        ///     called during deserialization.
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        public dynamic CreateQuery(Type elementType)
        {
            var ienumerable = fnGetObjects(elementType);
            Type enumerableType = ienumerable.GetType();
            if (!typeof(IEnumerable<>).MakeGenericType(elementType).IsAssignableFrom(enumerableType))
                ienumerable = Enumerable.ToArray(LinqHelper.CastToGenericEnumerable(ienumerable, elementType));


            IQueryable queryable = Queryable.AsQueryable(ienumerable);
            var provider = queryable.Provider;
            var queryType = typeof(Query<>).MakeGenericType(elementType);
            var ctor = queryType.GetConstructors()[2]; //Query(IQueryProvider provider, Expression expression)
            ParameterExpression[] parameters =
            {
                Expression.Parameter(typeof(IQueryProvider)),
                Expression.Parameter(typeof(Expression))
            };

            var newexpr = Expression.New(ctor, parameters);
            var lambda = Expression.Lambda(newexpr, parameters);
            var newFn = lambda.Compile();
            dynamic query = newFn.DynamicInvoke(provider, Expression.Constant(queryable));
            return query;
        }

        /// <summary>
        ///     This method is important to how this IQueryProvider works, for returning the IEnumerable from
        ///     which we generate the IQueryable and IQueryProvider to delegate the Execute(Expression) call to.
        ///     In practice this would be a call to the DAL.
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        internal static dynamic GetIEnumerableOf(Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = CreateDefaultInstance(listType);
            for (var i = 0; i < 10; i++)
            {
                var instance = CreateDefaultInstance(elementType);
                list.Add(instance);
            }
            return list;
        }

        /// <summary>
        ///     creates instance using default ctor
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static dynamic CreateDefaultInstance(Type type)
        {
            //default ctor:
            var ctor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Count() == 0);
            if (ctor == null)
                throw new ArgumentException(
                    string.Format("The type {0} must have a default (parameterless) constructor!", type));

            var newexpr = Expression.New(ctor);
            var lambda = Expression.Lambda(newexpr);
            var newFn = lambda.Compile();
            return newFn.DynamicInvoke();
        }

        #endregion
    }
}