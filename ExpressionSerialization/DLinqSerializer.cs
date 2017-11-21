using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;

namespace ExpressionSerialization
{
    /// <summary>
    ///     dependency : System.Data.Linq (DLINQ)
    /// </summary>
    public static class DLinqSerializer
    {
        public static XElement SerializeQuery(this IQueryable query)
        {
            var resolver = new TypeResolver(null, new[] {query.ElementType});
            var serializer = new ExpressionSerializer(resolver)
            {
                Converters = {new DLinqCustomExpressionXmlConverter(null, resolver)}
            };
            return serializer.Serialize(query.Expression);
        }

        public static IQueryable DeserializeQuery(this DataContext dc, XElement rootXml)
        {
            var resolver = new TypeResolver(null, GetKnownTypesFromTables(dc));
            var customConverter = new DLinqCustomExpressionXmlConverter(dc, resolver);
            var serializer =
                new ExpressionSerializer(resolver, new List<CustomExpressionXmlConverter> {customConverter});
            var queryExpr = serializer.Deserialize(rootXml);
            // Query kind is populated by the ResolveXmlFromExpression method
            if (customConverter.QueryKind == null)
                throw new Exception(
                    string.Format("CAnnot deserialize into DLinq query for datacontext {0} - no Table found", dc));
            return customConverter.QueryKind.Provider.CreateQuery(queryExpr);
        }

        private static IEnumerable<Type> GetKnownTypesFromTables(DataContext dc)
        {
            var dataContextTableTypes = new HashSet<Type>(dc.Mapping.GetTables().Select(mt => mt.RowType.Type));
            var entityTypes = new List<Type>(dataContextTableTypes);
            foreach (var tableType in dataContextTableTypes)
                entityTypes.Add(typeof(EntitySet<>).MakeGenericType(tableType));
            return entityTypes;
        }
    }

    internal class DLinqCustomExpressionXmlConverter : CustomExpressionXmlConverter
    {
        private readonly DataContext dc;
        private readonly TypeResolver resolver;

        public DLinqCustomExpressionXmlConverter(DataContext dc, TypeResolver resolver)
        {
            this.dc = dc;
            this.resolver = resolver;
        }

        public IQueryable QueryKind { get; private set; }

        public override bool TryDeserialize(XElement expressionXml, out Expression e)
        {
            if (expressionXml.Name.LocalName == "Table")
            {
                var type = resolver.GetType(expressionXml.Attribute("Type").Value);
                var table = dc.GetTable(type);
                // REturning a random IQueryable of the right kind so that we can re-create the IQueryable
                // instance at the end of this method...
                QueryKind = table;
                e = Expression.Constant(table);
                return true;
            }
            e = null;
            return false;
        }

        public override bool TrySerialize(Expression expression, out XElement x)
        {
            if (typeof(IQueryService).IsAssignableFrom(expression.Type))
            {
                x = new XElement("Table",
                    new XAttribute("Type", expression.Type.GetGenericArguments()[0].FullName));
                return true;
            }
            x = null;
            return false;
        }
    }
}