using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Net;

using RestSharp;
using RestSharp.Deserializers;

namespace WebApi
{
    public static class FormUpload
    {
        private static readonly Encoding encoding = Encoding.UTF8;
        public static HttpWebResponse MultipartFormDataPost(string postUrl, string userAgent, Dictionary<string, object> postParameters)
        {
            string formDataBoundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            return PostForm(postUrl, userAgent, contentType, formData);
        }
        private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, byte[] formData)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.UserAgent = userAgent;
            request.CookieContainer = new CookieContainer();
            request.ContentLength = formData.Length;

            // You could add authentication here as well if needed:
             request.PreAuthenticate = true;
             request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
            // request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("username" + ":" + "password")));
            request.Headers.Add("Authorization", "OAuth realm=\"Scrive\",oauth_consumer_key=\"39f5d791198928e3_1232\",oauth_token=\"fffb3c876d258cbc_1190\",oauth_signature_method=\"PLAINTEXT\",oauth_timestamp=\"1555220078\",oauth_nonce=\"f32pUKaGZlQ\",oauth_version=\"1.0\",oauth_signature=\"87b2b26d42f6a0e9%2659f352f7358f1ff4\"");
            // Send the form data to the request.
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData, 0, formData.Length);
                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter)
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;

                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }
        }
    }

    internal class Program
    {

        private static string _endPointPrefix = "https://api-testbed.scrive.com";
        private static string _oauth_consumer_key = "39f5d791198928e3_1232";
        private static string _oauth_token = "fffb3c876d258cbc_1190";
        private static string _clientCredentialSecret = "87b2b26d42f6a0e9";
        private static string _tokenCredentialSecret = "59f352f7358f1ff4";
        private static string _oauth_signature = _clientCredentialSecret + "&" + _tokenCredentialSecret;
        private static string _authorization = "oauth_signature_method=\"PLAINTEXT\",oauth_consumer_key=\"" + _oauth_consumer_key + "\",oauth_token=\"" + _oauth_token + "\",oauth_signature=\"" + _oauth_signature + "\"";
        private static string document_id = string.Empty;
        private static string _endPointSufix = string.Empty;

        public static void Main(string[] args)
        {

            // NEW 
            _endPointSufix = "/api/v2/documents/new";
            var content = Consume(_endPointSufix, "POST");
            var result = JObject.Parse(content);
            document_id = (string)result["id"];

        }

        static byte[] file_get_byte_contents(string fileName)
        {
            byte[] sContents;
            if (fileName.ToLower().IndexOf("http:") > -1)
            {
                // URL 
                System.Net.WebClient wc = new System.Net.WebClient();
                sContents = wc.DownloadData(fileName);
            }
            else
            {
                // Get file size
                FileInfo fi = new FileInfo(fileName);

                // Disk
                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                BinaryReader br = new BinaryReader(fs);
                sContents = br.ReadBytes((int)fi.Length);
                br.Close();
                fs.Close();
            }

            return sContents;
        }

        public static string Consume(string endPointSufix, string requestMethod)
        {
            /*
            var path = "D:\\1.pdf";
            string filevalue = System.Convert.ToBase64String(file_get_byte_contents(path));
            var content = string.Empty;
            var endPoint = _endPointPrefix + endPointSufix;

            var client = new RestClient("https://api-testbed.scrive.com/api/v2/documents/new");
            var request = new RestRequest(Method.POST);
            request.AddHeader("Postman-Token", "94a968f9-256f-440d-ace5-be42d5f4d84a");
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("Authorization", "OAuth realm=\"Scrive\",oauth_consumer_key=\"39f5d791198928e3_1232\",oauth_token=\"fffb3c876d258cbc_1190\",oauth_signature_method=\"PLAINTEXT\",oauth_timestamp=\"1555190424\",oauth_nonce=\"7ZhuKqXXfRP\",oauth_version=\"1.0\",oauth_signature=\"87b2b26d42f6a0e9%2659f352f7358f1ff4\"");
            request.AddHeader("content-type", "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW");
            */
            /*
            string parameter1 = "------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; ";
            parameter1 += "file=";
            parameter1 += filevalue;
            parameter1 += "; ";
            parameter1 += "filename=";
            parameter1 += "\"D:\\1.pdf\"";
            parameter1 += ";\r\nContent-Type: application/pdf\r\n\r\n\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"saved\"\r\n\r\ntrue\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW--";

            request.AddParameter("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW", parameter1, ParameterType.RequestBody);
            request.AddParameter("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW", "------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"file\"; filename=\"D:/1.pdf\"\r\nContent-Type: application/pdf\r\n\r\n\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW\r\nContent-Disposition: form-data; name=\"saved\"\r\n\r\ntrue\r\n------WebKitFormBoundary7MA4YWxkTrZu0gW--", ParameterType.RequestBody);
            //IRestResponse response = client.Execute(request);

            return response.Content;
            */

            // Read file data
            FileStream fs = new FileStream("D:\\1.pdf", FileMode.Open, FileAccess.Read);
            byte[] data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();

            // Generate post objects
            Dictionary<string, object> postParameters = new Dictionary<string, object>();
            postParameters.Add("filename", "1.pdf");
            postParameters.Add("fileformat", "pdf");
            postParameters.Add("file", new FormUpload.FileParameter(data, "1.pdf", "application/pdf"));

            // Create request and receive response
            string postURL = "https://api-testbed.scrive.com/api/v2/documents/new";
            string userAgent = "Someone";
            HttpWebResponse webResponse = FormUpload.MultipartFormDataPost(postURL, userAgent, postParameters);

            // Process response
            StreamReader responseReader = new StreamReader(webResponse.GetResponseStream());
            string fullResponse = responseReader.ReadToEnd();
            webResponse.Close();
            //Response.Write(fullResponse);
            return fullResponse;
        }
    }
}
