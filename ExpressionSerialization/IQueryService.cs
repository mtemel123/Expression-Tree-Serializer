using System.ServiceModel;
using System.ServiceModel.Web;
using System.Xml.Linq;

namespace ExpressionSerialization
{
    /// <summary>
    ///     WCF Web HTTP (REST) query service.
    ///     Derive your ServiceContract from this.
    /// </summary>
    [ServiceContract]
    public interface IQueryService
    {
        [OperationContract]
        [WebInvoke(Method = "POST",
            UriTemplate = "/execute",
            RequestFormat = WebMessageFormat.Xml,
            ResponseFormat = WebMessageFormat.Xml,
            BodyStyle = WebMessageBodyStyle.Bare)]
        object[] ExecuteQuery(XElement xml);
    }
}