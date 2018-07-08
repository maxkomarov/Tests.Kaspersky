using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
namespace HttpTest
{

    public class HttpTest
    {
        /// <summary>
        /// Осуществляет попытку входа с указанными в UserState учетными данными
        /// </summary>
        /// <param name="userState">Экземпляр UserState</param>
        /// <param name="thenLogout">Автоматически выполнять логофф после логона</param>
        /// <returns>Измененый экземпляр UserState</returns>
        public static UserState Login(UserState userState, bool thenLogout)
        {
            if (userState == null)
                throw new Exception("Аргумент userState не может быть null");

            userState.Result = false;

            CookieCollection tmp = userState.Cookie;

            Uri uri = new Uri("http://my.kaspersky.com/");

            if (String.IsNullOrEmpty(userState.SessionId))
            {
                //Сессия не была открыта ранее, поэтому открываем сами
                userState = Navigate(uri, HttpRequestTypeEnum.GET, userState);
                tmp = userState.Cookie;
            }

            #region Первая фаза - start

            #region Запрос OPTIONS для первой фазы
            Console.WriteLine("=====================================================================");
            Console.WriteLine("= Прцедура авторизации");
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 1. Logon/Start");
            Console.WriteLine("---------------------------------------------------------------------");

            //Отправляем OPTIONS для первой фазы авторизации
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");

            uri = new Uri("https://hq.uis.kaspersky.com/v3/logon/start");
            userState = Navigate(uri, HttpRequestTypeEnum.OPTIONS, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            //Проверяем на разрешение отправки POST
            if (userState.ResponseHeaders["Access-Control-Allow-Methods"] != null)
                if (!userState.ResponseHeaders["Access-Control-Allow-Methods"].ToLower().Contains("post"))
                    throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер не разрешил отправку POST");
            #endregion

            #region Шлем POST для первой фазы
            
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");
            userState.RequestHeaders.Add("Content-Type", "application/json");

            string postData = "{\"Realm\":\"https://center.kaspersky.com/\"}"; //??ОТкуда взялся реалм??

            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}");

            string logonContext = string.Empty;
            try
            {
                logonContext = JObject.Parse(userState.ResponseText).GetValue("LogonContext").ToString();
                Console.WriteLine();
                Console.WriteLine($"LogonContext = {logonContext}");
            }
            catch (Exception e)
            {
                throw new Exception($"Сервер вернул строку JSON  в некорректном формате: {userState.ResponseText}");
            }
            Console.WriteLine();
            #endregion

            #endregion

            #region Вторая фаза - proceed

            #region Запрос OPTIONS для второй фазы
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 2. Logon/Proceed");
            Console.WriteLine("---------------------------------------------------------------------");
            
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");

            uri = new Uri("https://hq.uis.kaspersky.com/v3/logon/proceed");
            userState = Navigate(uri, HttpRequestTypeEnum.OPTIONS, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            if (userState.ResponseHeaders["Access-Control-Allow-Methods"] != null)
                if (!userState.ResponseHeaders["Access-Control-Allow-Methods"].ToLower().Contains("post"))
                    throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер не разрешил отправку POST");

            #endregion

            #region Запрос POST для второй фазы
            
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");
            userState.RequestHeaders.Add("Content-Type", "application/json");

            postData = "{" + $"\"captchaType\":\"recaptcha\",\"captchaAnswer\":\"\",\"logonContext\":\"{logonContext}\",\"login\":\"{userState.UserName}\",\"password\":\"{userState.Password}\",\"locale\":\"ru\"" + "}";

            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}");

            string loginStatus = string.Empty;
            try
            {
                loginStatus = JObject.Parse(userState.ResponseText).GetValue("Status").ToString();
                if (loginStatus.ToLower() != "success")
                {
                    Console.WriteLine($"Попытка входа неуспешна: {loginStatus}");
                    return userState;
                }
                else
                    Console.WriteLine("Авторизация прошла успешно!");
            }
            catch (Exception e)
            {
                throw new Exception($"Сервер вернул строку JSON  в некорректном формате: {userState.ResponseText}\r\nИсключение:{e.Message}");
            }
           

            #endregion

            #endregion

            #region  Третья фаза - complete_active

            #region Запрос OPTIONS для третьей фазы
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 3. Logon/CompleteActive");
            Console.WriteLine("---------------------------------------------------------------------");
            
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");

            uri = new Uri("https://hq.uis.kaspersky.com/v3/logon/complete_active");
            userState = Navigate(uri, HttpRequestTypeEnum.OPTIONS, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            if (userState.ResponseHeaders["Access-Control-Allow-Methods"] != null)
                if (!userState.ResponseHeaders["Access-Control-Allow-Methods"].ToLower().Contains("post"))
                    throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер не разрешил отправку POST");
            #endregion

            #region Запрос POST для третьей фазы

             userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");
            userState.RequestHeaders.Add("Content-Type", "application/json");
            userState.RequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            userState.RequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

            postData = new StringBuilder("{" + $"\"logonContext\":\"{logonContext}\",\"TokenType\":\"SamlDeflate\"" + "}").ToString();

            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            try
            {
                JObject jo = JObject.Parse(userState.ResponseText);
                userState.UserToken = (jo.GetValue("UserToken").ToString() + "&returnUrl=http%3A%2F%2Fmy.kaspersky.com%2F&resendActivationLink=false");
            }
            catch (Exception e)
            {
                throw new Exception($"Сервер вернул строку JSON  в некорректном формате: {userState.ResponseText}", e);
            }
            #endregion

            #endregion

            #region Четвертая фаза - CompleteRestLogon
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 4. SignIn/CompleteRestLogon");
            Console.WriteLine("---------------------------------------------------------------------");
 
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            userState.RequestHeaders.Add("Accept", "*/*");
            userState.RequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            userState.RequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            userState.RequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36");

            userState.RequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            userState.Cookie = tmp;

            postData = new StringBuilder($"samlDeflatedToken={userState.UserToken}").ToString().Replace("+", "%2B").Replace("/","%2F"); ;

            uri = new Uri("https://my.kaspersky.com/SignIn/CompleteRestLogon");
            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK & userState.ResponseStatusCode != HttpStatusCode.Accepted)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            string returnUrl = string.Empty;
            try
            {
                JObject jo = JObject.Parse(userState.ResponseText);
                if (jo.ContainsKey("returnUrl"))
                    returnUrl = jo.GetValue("returnUrl").ToString();
                else
                    throw new Exception($"Сервер не вернул адрес возврата, как ожидалось. Resposetext:{userState.ResponseText}");

            }
            catch (Exception e)
            {
                throw new Exception($"Сервер вернул строку JSON в некорректном формате: {userState.ResponseText}", e);
            }

            #endregion

            #region Пятая фаза - заход по Url возврата, чтобы получить в кукисах UniqueContextHash
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 5. Возврат за UniqueContextHash");
            Console.WriteLine("---------------------------------------------------------------------");
            //Там, если по честному, c рута редирект стоит на Dashboard, но мы его кодировать не будем

            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Accept", "text / html,application / xhtml + xml,application / xml; q = 0.9,image / webp,image / apng,*/*;q=0.8");
            userState.RequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            userState.RequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            userState.RequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            userState.RequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36");

            userState.RequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            userState.Cookie.Add(tmp);
            tmp = userState.Cookie;

            uri = new Uri("https://my.kaspersky.com/dashboard");
            userState = Navigate(uri, HttpRequestTypeEnum.GET, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK & userState.ResponseStatusCode != HttpStatusCode.Accepted)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            bool exists = false;
            foreach (Cookie cookie in userState.Cookie)
                if (cookie.Name == "UniqueContextHash")
                {
                    exists = true;
                    break;
                }

            if (!exists)
                throw new Exception("Не удалось получить UniqueContextHash от веб-сервера");

            #endregion

            #region Шестая фаза - MyAccountApi (чтобы взять имя пользователя)
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 6. /MyAccountApi");
            Console.WriteLine("---------------------------------------------------------------------");

            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            userState.RequestHeaders.Add("Accept", "application/json, text/plain, */*");
            userState.RequestHeaders.Add("Accept-Encoding", "identity");
            userState.RequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            userState.RequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36");
            userState.RequestHeaders.Add("Content-Type", "application/json; charset=UTF-8");

            userState.Cookie.Add(tmp);
            tmp = userState.Cookie;

            postData = new StringBuilder("{}").ToString(); 

            uri = new Uri("https://my.kaspersky.com/MyAccountApi");
            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            //Забираем из JSON поле CurrentAlias
            try
            {
                JObject jo = JObject.Parse(userState.ResponseText);

                if (jo.ContainsKey("CurrentAlias"))
                    userState.CurrentAlias = jo.GetValue("CurrentAlias").ToString();
                else
                    throw new Exception($"Сервер не вернул поле CurrentAlias, как ожидалось. Resposetext:{userState.ResponseText}");

                userState.Result = true;
                Console.WriteLine($"Из профиля пользователя извлечено поле CurrentAlias, значение: {userState.CurrentAlias}");

            }
            catch (Exception e)
            {
                throw new Exception($"Сервер вернул строку JSON в некорректном формате: {userState.ResponseText}", e);
            }

            #endregion

            if (thenLogout)
                userState = Logout(userState);

            return userState;
        }

        /// <summary>
        /// Завершение сеанса работы
        /// </summary>
        /// <param name="userState">Экземпляр UserState, содержащий данные о текущей сессии авторизации</param>
        /// <returns>Изменений экземпляр UserState</returns>
        private static UserState Logout(UserState userState)
        {
            userState.Result = false;
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 7. Logout");
            Console.WriteLine("---------------------------------------------------------------------");
            
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            userState.RequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q= 0.9,image/webp,image/apng,*/*;q=0.8");
            userState.RequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            userState.RequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            userState.RequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36");
            userState.RequestHeaders.Add("Content-Type", "application/json; charset=UTF-8");


            string postData = new StringBuilder($"__RequestVerificationToken={userState.RequestVerificationToken}").ToString();

            Uri uri = new Uri("https://my.kaspersky.com/SignIn/SignOutTo");
            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}");
            userState.Result = true;

            return userState;
        }

        /// <summary>
        /// Осуществляет попытку загрузки web-ресурса
        /// </summary>
        /// <param name="href">""Экземпляр Uri, представляющий гиперссылку на резапрашиваемый ресурс</param>
        /// <param name="requestType">Тип запроса</param>
        /// <param name="userState">Детали запроса - заголовки, кукисы итп</param>
        /// <param name="postData">Данные, отправляемые в Payload запроса</param>
        /// <returns>Изменений экземпляр UserState</returns>
        public static UserState Navigate(Uri href, HttpRequestTypeEnum requestType, UserState userState, string postData = "")
        {
            userState.Result = false;
            Console.Write($"{ requestType}::{href.AbsoluteUri}.....");

            HttpWebRequest request = WebRequest.CreateHttp(href);
            request.ServicePoint.Expect100Continue = false;
            request.ServicePoint.SetTcpKeepAlive(true, 36000000, 1000);

            request.CookieContainer = new CookieContainer();
            foreach (Cookie cookie in userState.Cookie)
                request.CookieContainer.Add(cookie);

            request.Method = requestType.ToString();

            if (userState.RequestHeaders?.Get("Referer") != null)
            {
                request.Referer = userState.RequestHeaders.Get("Referer");
                userState.RequestHeaders.Remove("Referer");
            }

            if (userState.RequestHeaders?.Get("Content-Type") != null)
            {
                request.ContentType = userState.RequestHeaders.Get("Content-Type");
                userState.RequestHeaders.Remove("Content-Type");
            }

            if (userState.RequestHeaders?.Get("Accept") != null)
            {
                request.Accept = userState.RequestHeaders.Get("Accept");
                userState.RequestHeaders.Remove("Accept");
            }

            if (userState.RequestHeaders?.Get("User-Agent") != null)
            {
                request.UserAgent = userState.RequestHeaders.Get("User-Agent");
                userState.RequestHeaders.Remove("User-Agent");
            }

            foreach (string key in userState.RequestHeaders?.AllKeys)
                request.Headers.Add(key, userState.RequestHeaders.Get(key));

            if (postData != string.Empty)
            { 
                byte[] data = new ASCIIEncoding().GetBytes(postData);
                request.ContentLength = data.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                    stream.Close();
                }
                
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    userState.Cookie = response.Cookies;

                    foreach (Cookie cookie in userState.Cookie)
                    {
                        if (cookie.Name.ToLower() == "sessionid")
                            userState.SessionId = cookie.Value;
                        else if (cookie.Name.ToLower() == "__requestverificationtoken")
                            userState.RequestVerificationToken = cookie.Value;
                        else if (cookie.Name.ToLower() == "mykfedauth")
                            userState.MyKFedAuth = cookie.Value;
                    }

                    userState.ResponseStatus = response.StatusDescription;
                    userState.ResponseStatusCode = response.StatusCode;
                    userState.ResponseHeaders = response.Headers;

                    using (Stream receiveStream = response.GetResponseStream())
                    {
                        using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
                        {
                            userState.ResponseText = readStream.ReadToEnd();
                            readStream.Close();
                        }
                        receiveStream.Close();
                    }
                    Console.WriteLine($"{userState.ResponseStatusCode}");
                    userState.Result = true;
                }
            }
            catch (WebException wex)
            {
                Console.WriteLine($"Failed");
                Console.WriteLine($"{wex.Message}");
                userState.RequestStatus = RequestStatusEnum.Error;
                userState.ErrorMessage = wex.Message;
                if (wex.Message.Contains ("401"))
                    userState.ResponseText = "{\"Status\":\"InvalidRegistrationData\",\"StatusExtension\":\"CaptchaRequired\"}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed");
                Console.WriteLine($"{ex.Message}");
                userState.RequestStatus = RequestStatusEnum.Error;
                userState.ErrorMessage = ex.Message;
            }
            return userState;
        }
    }
}
