//-----------------------------------------------------------------------
// <copyright file="TJLeasingGateway.cs" company="OJSC Bank Eskhata">
//     Copyright (c) Bank Eskhata. All rights reserved.
// </copyright>
// <summary>
//     Gateway Эсхата Онлайн - Лизинг.
//     Developer: Ayubkhon Mamadov together with Shodiev Azizjon
//     Date:11.08.2023 ver.1.0.0.0
// </summary>
//-----------------------------------------------------------------------

namespace Gateways.TJLeasing
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
	using System.Net.Mime;
	using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
	using DPS.Common.Tools;
	using DPS.Domain;
	using GatewayInterface;
	using Gateways.Utils;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Класс реализующий шлюз.
    /// </summary>
    public partial class TJLeasingGateway : BaseGateway
	{
        

		/// <summary>
		/// Кодировка для запроса/ответа.*
		/// </summary>
		private static readonly Encoding Encoding = Encoding.UTF8;

        //*-
        protected static readonly Dictionary<int, int> ToCyberErrorMap = new Dictionary<int, int>
        {
            { 200, 0 },
            { 404, 711 },
            { 500, 30 },  //Ошибочный
		};

        //*-
        public static CyberplatError OperatorToCyberError(int antCode)
        {
            return ToCyberErrorMap.TryGetValue(antCode, out int cyberError) ? (CyberplatError)cyberError : CyberplatError.PaymentSystemError;
        }


        /// <summary>
        /// URL сервера.*
        /// </summary>
        private string url="";

        /// <summary>
        /// Token.*
        /// </summary>
        private string token;

		/// <summary>
		/// Тестовый идентификатор тестового контракта.*
		/// </summary>
		private string testaccount;

        /// <summary>
        /// Пользователь.* 
        /// </summary>
        private string user;

        /// <summary>
		/// Секретный ключ для подписи.*
		/// </summary>
		private string password;

        // ok
        public override string CheckSettings()
		{
            string message = "Error";
            try
            {
                StringList requestResult = AccountInfo(this.testaccount);
                if (requestResult["httpcode"] == "200")
                {
                    message = CheckSettingsResponse;
                }
                else
                {
                    message = requestResult["responsetext"];
                }

            }
            catch (Exception ex)
            {
                message += " (Exception): " + ex.Message;
            }
            return message;
        }

        /// <summary>
        /// Интерактивная проверка исходных данных для платежа
        /// </summary>
        /// <param name="paymentData">Данные платежа</param>
        /// <param name="operatorData">Данные оператора</param>
        /// <returns><see cref="OnlineCheckResponse" />, как результат интерактивной проверки</returns>
        public override OnlineCheckResponse ProcessOnlineCheck(NewPaymentData paymentData, object operatorData)
		{
            var checkResponse = new OnlineCheckResponse();

            var operatorRow = operatorData as DataRow;
            string operatorFormatString = operatorRow["OsmpFormatString"].ToString();
            string formatedPaymentParams = FormatParameters(paymentData.Params, operatorFormatString);
            var parameters = new StringList(formatedPaymentParams, ";");


            string id  = parameters["ID"].Replace(" ", string.Empty);
            StringList result = AccountInfo(id);

            int errorCode = int.Parse(result["httpcode"]);
            checkResponse.CyberplatError = OperatorToCyberError(errorCode);
            log("Online check (ID={0}) errorCode={1}", id, errorCode);

            if (errorCode == 200)
            {
                
                // Успешная информация об абоненте
                dynamic otvet = JsonConvert.DeserializeObject(result["responsetext"]);
                checkResponse.ExtraParams += "debt=" + otvet.debt["debt"] + "\r\n";
                checkResponse.ExtraParams += "name=" + otvet.result["name"] + "\r\n";
            }

            return checkResponse;

        }

        /// <summary>
        /// Инициализирует шлюз.
        /// </summary>
        /// <param name="data">Данные в xml формате, по шаблону InitializeTemplate.</param>
        protected override void Initialize(string data)
		{
			string identifierInfo = "GateProfileID=" + this.GateProfileID.ToString(CultureInfo.CurrentCulture);

			this.log("Initialize, " + identifierInfo);

			try
			{
				var settings = new XmlGatewaySettings(data);

				this.url = settings.SingleOrDefault("url", string.Empty);
                this.testaccount= settings.SingleOrDefault("test_account", string.Empty);
                this.email = settings.SingleOrDefault("email", string.Empty);
                this.password = settings.SingleOrDefault("password", string.Empty);
                this.token = this.GetToken();
                if (this.EmulatorEnabled)
				{
					this.HttpSender = new Emulator.HttpSenderEmulator
					{
						ProxySettings = new ProxySettings(),
						Logger = this.Logger,
						RootFolder = BaseGateway.SharedFolder
					};

					this.log($"!!! ВКЛЮЧЕН РЕЖИМ ЭМУЛЯЦИИ !!! ({identifierInfo})");
				}
			}
			catch (Exception ex)
			{
				this.log("Initialize exception: " + ex);
				throw;
			}
		}

        /// <summary>
        /// Получение token
        /// </summary>
        /// <returns></returns>
        private string GetToken()
        {
            string token = "";
            StringList requestResult = Authentication(this.user, this.password);
            if (requestResult["httpcode"] == "200")
            {
                dynamic otvet = JsonConvert.DeserializeObject(requestResult["responsetext"]);

                token = "Bearer " + otvet.access_token;
                log($"GetToken Success (code={requestResult["httpcode"] })");
            }
            else
            {
                log($"GetToken Error (code={requestResult["httpcode"] })");
            }

            return token;
        }

        /// <summary>
        /// Генерация токена
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public StringList Authentication(string user, string password)
        {
            Parameters param = new Parameters();
            param.Params = new Dictionary<string, string>
            {
                { "user", "\"" + user + "\"" },
                { "password", "\"" + password + "\"" }

            };
            string url = this.url + "/LizingAPI/GetToken";
            try
            {
                return this.RequestGet(url, param.GetParams(), "GetToken");
            }
            catch (Exception ex)
            {
                this.log("Error on GetToken threw exception:{0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Получение информации о счетах
        /// </summary>
        /// <param name="account">Номер лицевой счет</param>
        /// <returns></returns>
        public StringList AccountInfo(string id)
        {
            string url = this.url + "/LizingAPI/GetClient?id=" + id;
            try
            {
                return this.RequestGet(url, "AccountInfo");
            }
            catch (Exception ex)
            {
                this.log("AccountInfo threw exception:{0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Создание платежа
        /// </summary>
        /// <param name="account"></param>
        /// <param name="sum"></param>
        /// <param name="tranid"></param>
        /// <returns></returns>
        public StringList InitPayment(string id, double sum)
        {
            Parameters param = new Parameters();
            param.Params = new Dictionary<string, string>
            {
                { "account_no", "\"" + id + "\""},
                { "amount", Tools.ftos(sum)},
            };
            string url = this.url + "/LizingAPI/Payment";
            try
            {
                return this.RequestPost(url, param.GetParams(), "Transaction");
            }
            catch (Exception ex)
            {
                this.log("Error on InitPayment threw exception:{0}", ex.Message);
                throw;
            }
        }


        public StringList RequestGet(string urlstr, string action)
        {
            HttpWebResponse myWebResponse = null;
            HttpStatusCode statusCode;
            string responsetxt = string.Empty;
            DetailLog(this.DetailLogEnabled, $"Request({action}){ThreadInfo} {urlstr}");
            WebRequest reqGET = WebRequest.Create(urlstr);
            reqGET.Method = "GET";
            if(action = "GetToken")
            {
                // Установка Basic Authentication заголовка
            string authHeaderValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(user + ":" + password));
            reqGET.Headers["Authorization"] = "Basic " + authHeaderValue;
            }
            else
            {
            reqGET.Headers.Add("Authorization", this.token);
            }
            try
            {
                myWebResponse = (HttpWebResponse)reqGET.GetResponse();
            }
            catch (WebException we)
            {
                myWebResponse = (HttpWebResponse)we.Response;
            }
            statusCode = myWebResponse.StatusCode;
            int httpstatuscode = (int)statusCode;
            var responseStream = myWebResponse.GetResponseStream();
            var myStreamReader = new StreamReader(responseStream, Encoding.UTF8); //Encoding.Default
            var responseString = myStreamReader.ReadToEnd();
            responsetxt = responseString;
            this.DetailLog(this.DetailLogEnabled, "Response({2}){0}: {1}", ThreadInfo, UnicodeToCyrillic(responseString), action);

            var response = new StringList("|")
            {
                { "httpcode", httpstatuscode.ToString()},
                { "responsetext", responsetxt }
            };
            return response;
        }

        public StringList RequestPost(string urlstr, string jsonstr, string action)
        {
            HttpWebResponse httpResponse = null;
            HttpStatusCode statusCode;
            string status = string.Empty;
            string responsetxt = string.Empty;

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(urlstr);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            if (action == "Transaction")
            {
                httpWebRequest.Headers.Add("Authorization", this.token);
            }

            string strrequest = jsonstr.Replace(this.password, "********");
            DetailLog(this.DetailLogEnabled, $"Request {action}{ThreadInfo}: {urlstr} {strrequest}");

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(jsonstr);
            }
            try
            {
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            }
            catch (WebException we)
            {
                httpResponse = (HttpWebResponse)we.Response;
            }
            statusCode = httpResponse.StatusCode;
            int httpstatuscode;

            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var responseText = streamReader.ReadToEnd();
                httpstatuscode = (int)statusCode;
                responsetxt = responseText.ToString();
                this.DetailLog(this.DetailLogEnabled, "Response {0}{1}{3}: {2}", action, ThreadInfo, TokenHide(UnicodeToCyrillic(responseText)), httpstatuscode);
            }

            var response = new StringList("|")
            {
                { "httpcode", httpstatuscode.ToString()},
                { "responsetext", responsetxt }
            };
            return response;
        }

        /// <summary>
        /// Переобразует символ юникод в кирилицу
        /// </summary>
        /// <param name="strText">Строка текста.</param>
        /// <returns>Строка с кодировкой кирилица.</returns>
        private static string UnicodeToCyrillic(string strText)
        {
            Regex reg = new Regex(@"(?i)\\[uU]([0-9a-f]{4})");
            return reg.Replace(strText, delegate (Match m) { return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString(); });
        }

        private static string HideText(string text)
        {
            return new string('*', text.Length);
        }
        private static string TokenHide(string text)
        {
            string otvet = JsonConvert.DeserializeObject(text).ToString();
            dynamic token_ = JsonConvert.DeserializeObject(text);
            if (otvet.Contains("token"))
            {
                string a = otvet.Replace(token_.token.ToString(), HideText(token_.token.ToString()));
                return a;
            }
            else
            {
                return text;
            }
        }

        /// <summary>
        /// Проверяет возможность осуществления платежа.
        /// </summary>
        /// <param name="payment"><see cref="PreprocessingPaymentRow"/>, содержащий все необходимые параметры платежа.</param>
        /// <param name="exData">Дополнительные данные.</param>
        protected override void Check(PreprocessingPaymentRow payment, object exData)
		{
		}

		/// <summary>
		/// Выполняет платеж.
		/// </summary>
		/// <param name="payment"><see cref="PreprocessingPaymentRow"/>, содержащий все необходимые параметры платежа.</param>
		protected override void Payment(PreprocessingPaymentRow payment)
		{
			string id = payment.FormattedParams["ID"];
            
            StringList requestResult = InitPayment(id, payment.Amount);

            int responseCode = int.Parse(requestResult["httpcode"]);

            log("{0}", responseCode);
			switch (responseCode)
			{
                case 200:
                    payment.Status = PaymentStatus.Completed;
                	break;
                case 404:
                case 500:
                    payment.Status = PaymentStatus.NotProcessed;
                    break;
				default:
					payment.Status = PaymentStatus.Unknown;
					break;
			}
		}

        class Parameters
        {
            public string SecretKey { get; set; }
            public Dictionary<string, string> Params { get; set; }
            public string GetParams()
            {
                string result = string.Empty;
                string splitter = string.Empty;
                int cnt = 0;
                foreach (var item in Params)
                {
                    cnt++;
                    splitter = string.Empty;
                    if (cnt < Params.Count()) splitter = ",";
                    result = result + string.Format("\"{0}\":{1}{2}", item.Key, item.Value, splitter);
                }
                return "{" + result + "}"; ;
            }
            public string GetHash(string input)
            {
                return Tools.GetMD5(input);
            }
        }

    }
}
