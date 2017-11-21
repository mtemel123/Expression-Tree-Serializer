using System;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;

namespace ExpressionSerialization
{
    public class QueryExpressionXmlConverter : CustomExpressionXmlConverter
    {
        private readonly QueryCreator creator;
        private readonly TypeResolver resolver;

        public QueryExpressionXmlConverter(QueryCreator creator = null, TypeResolver resolver = null)
        {
            this.creator = creator;
            this.resolver = resolver;
        }


        /// <summary>
        ///     Upon deserialization replace the Query (IQueryable) in the Expression tree with a new Query that has a different
        ///     ConstantExpression.
        ///     Re-create the Query, but with a different server-side IQueryProvider.
        ///     For IQueryProvder, we just create a Linq.EnumerableQuery`1.
        ///     Need both a working IQueryProvider and a new Query with ConstantExpression equal to actual data.
        /// </summary>
        /// <param name="expressionXml"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public override bool TryDeserialize(XElement expressionXml, out Expression e)
        {
            if (creator == null || resolver == null)
                throw new InvalidOperationException(string.Format(
                    "{0} and {1} instances are required for deserialization.", typeof(QueryCreator),
                    typeof(TypeResolver)));

            if (expressionXml.Name.LocalName == "Query")
            {
                var elementType = resolver.GetType(expressionXml.Attribute("elementType").Value);
                var query = creator.CreateQuery(elementType);
                e = Expression.Constant(query);
                return true;
            }
            e = null;
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="e"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        public override bool TrySerialize(Expression e, out XElement x)
        {
            if (e.NodeType == ExpressionType.Constant
                && typeof(IQueryable).IsAssignableFrom(e.Type))
            {
                var elementType = ((IQueryable) ((ConstantExpression) e).Value).ElementType;
                if (typeof(Query<>).MakeGenericType(elementType) == e.Type)
                {
                    x = new XElement("Query",
                        new XAttribute("elementType", elementType.FullName));
                    return true;
                }
            }
            x = null;
            return false;
        }
    }

    public abstract class CustomExpressionXmlConverter
    {
        public abstract bool TryDeserialize(XElement expressionXml, out Expression e);
        public abstract bool TrySerialize(Expression expression, out XElement x);
    }
}