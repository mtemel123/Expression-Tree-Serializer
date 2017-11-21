using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ExpressionSerialization
{
    public static class LinqHelper
    {
        public static IQueryable WhereCall(LambdaExpression wherePredicate,
            IEnumerable sourceCollection,
            Type elementType //the Type to cast TO
        )
        {
            IQueryable queryableData;
            queryableData = CastToGenericEnumerable(sourceCollection, elementType).AsQueryable();
            var whereCallExpression = Expression.Call(
                typeof(Queryable),
                "Where", //http://msdn.microsoft.com/en-us/library/bb535040
                new[] {elementType},
                queryableData.Expression, //this IQueryable<TSource> source				
                wherePredicate); //Expression<Func<TSource, bool>> predicate
            var results = queryableData.Provider.CreateQuery(whereCallExpression);
            return results;
        }

        /// <summary>
        ///     Casts a collection, at runtime, to a generic (or strongly-typed) collection.
        /// </summary>
        public static IEnumerable CastToGenericEnumerable(IEnumerable sourceobjects, Type TSubclass)
        {
            var queryable = sourceobjects.AsQueryable();
            var elementType = TSubclass;
            var castExpression
                = //Expression.Call(typeof(Queryable).GetMethod("Cast"),  Expression.Constant(elementType), Expression.Constant(queryable));// Expression.Call(typeof(System.Collections.IEnumerable),"Cast" , new Type[] { elementType }, Expression.Constant(objectsArray));
                Expression.Call(typeof(Queryable), "Cast", new[] {elementType}, Expression.Constant(queryable));
            var lambdaCast = Expression.Lambda(castExpression, Expression.Parameter(typeof(IEnumerable)));
            dynamic castresults = lambdaCast.Compile().DynamicInvoke(queryable);
            return castresults;
        }

        public static IList CastToGenericList(IEnumerable sourceobjects, Type elementType)
        {
            dynamic dynamicList = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            dynamic casted; //must be dynamic, NOT: System.Collections.IEnumerable casted;			
            casted = CastToGenericEnumerable(sourceobjects, elementType);
            foreach (var obj in casted)
                dynamicList.Add(obj);
            return dynamicList;
        }

        public static IEnumerable<TElement> WhereCall<TElement>(LambdaExpression wherePredicate,
            IEnumerable<TElement> sourceCollection = null)
        {
            IQueryable<TElement> queryableData;
            queryableData = sourceCollection.AsQueryable();

            var whereCallExpression = Expression.Call(
                typeof(Queryable),
                "Where", //http://msdn.microsoft.com/en-us/library/bb535040
                new[] {queryableData.ElementType}, //<TSource>
                queryableData.Expression, //this IQueryable<TSource> source				
                wherePredicate); //Expression<Func<TSource, bool>> predicate
            var results = queryableData.Provider.CreateQuery<TElement>(whereCallExpression);
            return results.ToArray();
        }


        /// <summary>
        ///     also see: http://stackoverflow.com/questions/5862266/how-is-a-funct-implicitly-converted-to-expressionfunct
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Expression<Func<T, TResult>> FuncToExpression<T, TResult>(Expression<Func<T, TResult>> func)
        {
            return func;
        }

        public static Expression<Func<TResult>> FuncToExpression<TResult>(Expression<Func<TResult>> func)
        {
            return func;
        }

        public static MemberExpression GetMemberAccess<T, TResult>(Expression<Func<T, TResult>> expr)
        {
            var mem = (MemberExpression) expr.Body;
            return mem;
        }

        public static MemberExpression GetMemberAccess<T>(Expression<Func<T>> expr)
        {
            var mem = (MemberExpression) expr.Body;
            return mem;
        }

        public static MethodCallExpression GetMethodCallExpression<T, TResult>(Expression<Func<T, TResult>> expr)
        {
            MethodCallExpression m;
            m = (MethodCallExpression) expr.Body;
            return m;
        }

        public static TResult Execute<TResult>(Expression expression)
        {
            var queryabledata = new TResult[0]
                .AsEnumerable().AsQueryable();
            IQueryProvider provider;
            provider = queryabledata.Provider;
            return provider.Execute<TResult>(expression);
        }

        public static D RunTimeConvert<D, S>(S src, Type convertExtension) where S : new()
        {
            return (D) RunTimeConvert(src, convertExtension);
        }

        public static dynamic RunTimeConvert(object instance, Type convertExtension)
        {
            var srcType = instance.GetType();
            var methodinfo = (from m in convertExtension.GetMethods()
                let parameters = m.GetParameters()
                where m.Name == "Convert"
                      && parameters.Any(p => p.ParameterType == srcType)
                select m).First();
            var castExpression = Expression.Call(methodinfo, Expression.Constant(instance));
            var lambdaCast = Expression.Lambda(castExpression, Expression.Parameter(srcType));
            dynamic castresults = lambdaCast.Compile().DynamicInvoke(instance);
            return castresults;
        }

        public static dynamic CreateInstance(this Type type)
        {
            //default ctor:
            var ctor = type.GetConstructors().First(c => c.GetParameters().Count() == 0);
            var newexpr = Expression.New(ctor);
            var lambda = Expression.Lambda(newexpr);
            var newFn = lambda.Compile();
            return newFn.DynamicInvoke();
        }
    }
}