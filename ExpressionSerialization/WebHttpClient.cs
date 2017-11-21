using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Threading;
using Resolver = ExpressionSerialization.TypeResolver;

namespace ExpressionSerialization
{
    public class WebHttpClient<TChannel> where TChannel : IQueryService
    {
        private readonly HashSet<Type> _knownTypes;

        public WebHttpClient(Uri baseAddress, IEnumerable<Type> knownTypes = null)
        {
            this.baseAddress = baseAddress;
            if (knownTypes == null)
                _knownTypes = new HashSet<Type>(GetServiceKnownTypes(typeof(TChannel)));
            else
                _knownTypes = new HashSet<Type>(GetServiceKnownTypes(typeof(TChannel)).Union(knownTypes));
        }

        public IEnumerable<Type> knownTypes => _knownTypes.AsEnumerable();
        public Uri baseAddress { get; }


        public TResult SynchronousCall<TResult>(Expression<Func<TChannel, object>> methodcall)
        {
            return (TResult) SynchronousCall(methodcall, typeof(TResult));
        }

        /// <summary>
        ///     Specifying the returnType is necessary because sometimes DataContractSerializer cannot deserialize the response
        ///     without knowing the exact Type of the response.
        ///     Sometimes DCS fails to deserialize, sometimes it doesn't, but the best practice is to always specify the expected
        ///     return Type.
        ///     If calling from SL, this call must be made from a thread separate from the main UI thread. For example,
        ///     enclose this call within a call to ThreadPool.QueueUserWorkItem.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="methodcall">Lambda Expression that is the call to a method on the WebHttp service.</param>
        /// <param name="returnType">expected Type returned in the response</param>
        /// <returns></returns>
        //public object SynchronousCall<TResult>(Expression<Func<TChannel, TResult>> methodcall, Type returnType = null)
        public object SynchronousCall(Expression<Func<TChannel, object>> methodcall, Type returnType)
        {
            object result = null;
            Stream stream = null;
            var m = (MethodCallExpression) methodcall.Body;
            var channelType = m.Object.Type;
            var same = returnType == m.Method.ReturnType;

            var baseTypes = Resolver.GetBaseTypes(returnType);
            foreach (var b in baseTypes)
                _knownTypes.Add(b);
            var parameters = GetParameters(m);

            string method;
            IOperationBehavior webattribute;
            OperationContractAttribute operationcontract;
            WebMessageFormat requestformat, responseformat;
            var endpoint = GetOperationInfo(m.Method, baseAddress, out method, out webattribute, out operationcontract,
                out requestformat, out responseformat);

            var reset = new ManualResetEvent(false);
            Action<Stream> completedHandler = s =>
            {
                stream = s;
                result = Deserialize(returnType, stream, responseformat);
                reset.Set();
            };
            CreateHttpWebRequest(endpoint, instance: parameters,
                callback: completedHandler,
                method: method,
                requestFormat: requestformat,
                responseFormat: responseformat);
            reset.WaitOne();
            return result;
            //TChannel service = default(TChannel);
            //return methodcall.Compile().Invoke(service);
        }


        private void CreateHttpWebRequest(Uri absoluteUri,
            object instance,
            Action<Stream> callback,
            string method = "POST",
            WebMessageFormat requestFormat = WebMessageFormat.Xml,
            WebMessageFormat responseFormat = WebMessageFormat.Xml)
        {
            Stream postStream;
            HttpWebRequest request;
            HttpWebResponse response;
#if SILVERLIGHT
			request = WebRequest.CreateHttp(absoluteUri);
#else
            request = WebRequest.Create(absoluteUri) as HttpWebRequest;
#endif
            request.Method = method;

            AsyncCallback responseCallback = ar2 =>
            {
                var request2 = (HttpWebRequest) ar2.AsyncState;
                response = (HttpWebResponse) request2.EndGetResponse(ar2);
                var stream = response.GetResponseStream();
                callback(stream);
                //stream.Position = 0;//NotSupportedException: Specified method is not supported.
            };

            if (method == "POST" && instance != null)
            {
                request.ContentType = requestFormat == WebMessageFormat.Json ? "application/json" : "application/xml";
                AsyncCallback requestCallback = ar1 =>
                {
                    postStream = request.EndGetRequestStream(ar1);
                    Serialize(postStream, instance, requestFormat);
                    postStream.Close();
                    request.BeginGetResponse(responseCallback, request); //GetResponse
                };
                request.BeginGetRequestStream(requestCallback, request);
            }
            else if (method == "GET")
            {
                request.ContentLength = 0;
                request.BeginGetResponse(responseCallback, request);
            }
        }

        private static dynamic GetParameters(MethodCallExpression m)
        {
            if (m.Arguments.Count == 0)
                return null;
            var argexp = m.Arguments[0];
            var evald = (ConstantExpression) Evaluator.PartialEval(argexp);
            var lambda = Expression.Lambda(evald);
            dynamic args = lambda.Compile().DynamicInvoke();
            return args;
        }

        private static Uri GetOperationInfo(MethodInfo operation,
            Uri baseAddress,
            out string method,
            out IOperationBehavior webbehavior, //out WebInvokeAttribute webinvoke,
            out OperationContractAttribute operationcontract,
            out WebMessageFormat requestformat,
            out WebMessageFormat responseformat)
        {
            var customAttributes = operation.GetCustomAttributes(false);
            webbehavior =
                customAttributes.Single(a => a is WebInvokeAttribute || a is WebGetAttribute) as IOperationBehavior;
            operationcontract =
                customAttributes.Single(a => a is OperationContractAttribute) as OperationContractAttribute;
            if (webbehavior is WebInvokeAttribute)
            {
                requestformat = ((WebInvokeAttribute) webbehavior).RequestFormat;
                responseformat = ((WebInvokeAttribute) webbehavior).ResponseFormat;
                var relative = new Uri(((WebInvokeAttribute) webbehavior).UriTemplate, UriKind.Relative);
                var endpoint = new Uri(baseAddress, relative);
                method = ((WebInvokeAttribute) webbehavior).Method;
                return endpoint;
            }
            if (webbehavior is WebGetAttribute)
            {
                requestformat = ((WebGetAttribute) webbehavior).RequestFormat;
                responseformat = ((WebGetAttribute) webbehavior).ResponseFormat;
                var relative = new Uri(((WebGetAttribute) webbehavior).UriTemplate, UriKind.Relative);
                var endpoint = new Uri(baseAddress, relative);
                method = "GET";
                return endpoint;
            }
            throw new NotSupportedException(webbehavior.GetType().FullName + " is not supported.");
        }

        private static IEnumerable<Type> GetServiceKnownTypes(Type service)
        {
            var knownTypes = new HashSet<Type>();
            var customattributes = service.GetCustomAttributes(true);
            var kattrs = customattributes.OfType<ServiceKnownTypeAttribute>();
            foreach (var k in kattrs)
                knownTypes.Add(k.Type);

            var methods = service.GetMethods();
            foreach (var m in methods)
            foreach (var k in m.GetCustomAttributes(true).OfType<ServiceKnownTypeAttribute>())
                knownTypes.Add(k.Type);
            return knownTypes;
        }

        #region serialization

        private long Serialize(Stream stream, object instance, WebMessageFormat requestFormat)
        {
            dynamic
                serializer; //DataContractJsonSerializer or DataContractSerializer (XmlObjectSerializer d.n.e. in SL)
            var elementType = instance.GetType();
            _knownTypes.Add(elementType);
            switch (requestFormat)
            {
                case WebMessageFormat.Json:
                    serializer = new DataContractJsonSerializer(elementType, _knownTypes);
                    break;
                case WebMessageFormat.Xml:
                    serializer = new DataContractSerializer(elementType, _knownTypes);
                    break;
                default:
                    serializer = new DataContractSerializer(elementType, _knownTypes);
                    break;
            }
            serializer.WriteObject(stream, instance);
            return 0; //return stream.Length;
        }

        private object Deserialize(Type type, Stream stream, WebMessageFormat responseformat = WebMessageFormat.Json)
        {
            dynamic serializer;
            _knownTypes.Add(type);
            switch (responseformat)
            {
                case WebMessageFormat.Json:
                    serializer = new DataContractJsonSerializer(type, _knownTypes);
                    break;
                case WebMessageFormat.Xml:
                    serializer = new DataContractSerializer(type, _knownTypes);
                    break;
                default:
                    serializer = new DataContractJsonSerializer(type, _knownTypes);
                    break;
            }
            return serializer.ReadObject(stream);
        }

        private T Deserialize<T>(Stream stream, WebMessageFormat responseformat = WebMessageFormat.Json)
        {
            return (T) Deserialize(typeof(T), stream, responseformat);
        }

        #endregion
    }
}