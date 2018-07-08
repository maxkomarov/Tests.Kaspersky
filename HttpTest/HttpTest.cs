using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
namespace HttpTest
{

    public class HttpTest
    {
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
            //Откуда берется таргет для запроса?

            #region Запрос OPTIONS для первой фазы
            Console.WriteLine("=====================================================================");
            Console.WriteLine("= Прцедура авторизации");
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 1. Logon/Start");
            Console.WriteLine("---------------------------------------------------------------------");
            // >> OPTIONS https://hq.uis.kaspersky.com/v3/logon/start 
            // Запрашиваем разрешенные методы, но это для конкретного случая
            // можно и пропустить - все равно шлем POST
            //Host: hq.uis.kaspersky.com
            //Connection: keep - alive
            //Access - Control - Request - Method: POST
            //Origin: https://my.kaspersky.com
            //User - Agent: Mozilla / 5.0(Windows NT 6.1) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 67.0.3396.99 Safari / 537.36
            //Access - Control - Request - Headers: content - type
            //Accept: */*
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7

            //Отправляем OPTIONS для первой фазы авторизации
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");

            uri = new Uri("https://hq.uis.kaspersky.com/v3/logon/start");
            userState = Navigate(uri, HttpRequestTypeEnum.OPTIONS, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            //В ответ получаем
            //HTTP / 1.1 200 OK
            //Cache - Control: private
            // Server: nginx
            //X-UIS-TraceId: ba90dfe6-d8c3-4e70-83bf-8fe2a2e28fc0
            //Access-Control-Allow-Methods: POST,OPTIONS
            //Access-Control-Allow-Headers: Content-Type
            //Access-Control-Allow-Origin: https://my.kaspersky.com
            //Strict-Transport-Security: max-age=31536000
            //Date: Tue, 03 Jul 2018 12:30:19 GMT
            //Content-Length: 0

            //Проверяем на разрешение отправки POST
            if (userState.ResponseHeaders["Access-Control-Allow-Methods"] != null)
                if (!userState.ResponseHeaders["Access-Control-Allow-Methods"].ToLower().Contains("post"))
                    throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер не разрешил отправку POST");
            #endregion

            #region Шлем POST для первой фазы
            //POST https://hq.uis.kaspersky.com/v3/logon/start
            //Host: hq.uis.kaspersky.com
            //Connection: keep - alive
            //Content - Length: 41
            //Accept: */*
            //Origin: https://my.kaspersky.com
            //User-Agent: Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36
            //Content-Type: application/json
            //Referer: https://my.kaspersky.com/
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7
            //Откуда берется строка с именем области?
            //--JSON: {"Realm":"https://center.kaspersky.com/"}

            //Отправляем POST первой фазы авторизации
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");
            userState.RequestHeaders.Add("Content-Type", "application/json");

            string postData = "{\"Realm\":\"https://center.kaspersky.com/\"}"; //??ОТкуда взялся реалм??

            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}");

            //В ответ получаем
            //HTTP / 1.1 200 OK
            //Cache - Control: private
            //Content-Type: application/json; charset=utf-8
            //Server: nginx
            //X-UIS-TraceId: 31108803-220d-467b-8753-fae28d97a081
            //Access-Control-Allow-Methods: POST,OPTIONS
            //Access-Control-Allow-Headers: Content-Type
            //Access-Control-Allow-Origin: https://my.kaspersky.com
            //Strict-Transport-Security: max-age=31536000
            //Date: Tue, 03 Jul 2018 12:30:19 GMT
            //Content-Length: 191
            //--JSON: {"LogonContext":"onWbhMLrEZviwRJk7VR1iQ3hh1DaYq7JmpxeB5jUMbqaCTps_0CSAdHQOBnRYRu0GOLvEmBOFmsG3sqfR25ce4-IvPr-zG_RXUqPeLztx0tJEauYdic2L4qQCDjwEF6BcoyRLWaNiI3EJReA5GlHOVxddyvbgXH8cEgV9bXqA9o1"}

            //Теперь надо сохранить LogonContext
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
            //Отправляем OPTIONS https://hq.uis.kaspersky.com/v3/logon/proceed HTTP/1.1
            //Host: hq.uis.kaspersky.com
            //Connection: keep - alive
            //Access - Control - Request - Method: POST
            //Origin: https://my.kaspersky.com
            //User - Agent: Mozilla / 5.0(Windows NT 6.1) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 67.0.3396.99 Safari / 537.36
            //Access - Control - Request - Headers: content - type
            //Accept: */*
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7

            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");

            uri = new Uri("https://hq.uis.kaspersky.com/v3/logon/proceed");
            userState = Navigate(uri, HttpRequestTypeEnum.OPTIONS, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            //В ответ получаем
            //HTTP / 1.1 200 OK
            //Cache - Control: private
            //  Server: nginx
            //X-UIS-TraceId: 2a1a2d58-135d-4832-83ed-f2747b8019ce
            //Access-Control-Allow-Methods: POST,OPTIONS
            //Access-Control-Allow-Headers: Content-Type
            //Access-Control-Allow-Origin: https://my.kaspersky.com
            //Strict-Transport-Security: max-age=31536000
            //Date: Tue, 03 Jul 2018 12:30:31 GMT
            //Content-Length: 0

            //Проверяем на разрешение отправки POST
            if (userState.ResponseHeaders["Access-Control-Allow-Methods"] != null)
                if (!userState.ResponseHeaders["Access-Control-Allow-Methods"].ToLower().Contains("post"))
                    throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер не разрешил отправку POST");

            #endregion

            #region Запрос POST для второй фазы
            //Авторизуемся
            //POST https://hq.uis.kaspersky.com/v3/logon/proceed HTTP/1.1
            //Host: hq.uis.kaspersky.com
            //Connection: keep - alive
            //Content - Length: 302
            //Accept: */*
            //Origin: https://my.kaspersky.com
            //User-Agent: Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36
            //Content-Type: application/json
            //Referer: https://my.kaspersky.com/
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7
            //--JSON: {"captchaType":"recaptcha","captchaAnswer":"","logonContext":"onWbhMLrEZviwRJk7VR1iQ3hh1DaYq7JmpxeB5jUMbqaCTps_0CSAdHQOBnRYRu0GOLvEmBOFmsG3sqfR25ce4-IvPr-zG_RXUqPeLztx0tJEauYdic2L4qQCDjwEF6BcoyRLWaNiI3EJReA5GlHOVxddyvbgXH8cEgV9bXqA9o1","login":"????????????","password":"?????????","locale":"ru"}

            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Referer", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");
            userState.RequestHeaders.Add("Content-Type", "application/json");

            postData = "{" + $"\"captchaType\":\"recaptcha\",\"captchaAnswer\":\"\",\"logonContext\":\"{logonContext}\",\"login\":\"{userState.UserName}\",\"password\":\"{userState.Password}\",\"locale\":\"ru\"" + "}";

            userState = Navigate(uri, HttpRequestTypeEnum.POST, userState, postData);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на POST {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}");

            //Получаем ответ
            //HTTP / 1.1 200 OK
            //Cache - Control: private
            //Content-Type: application/json; charset=utf-8
            //Server: nginx
            //X-UIS-TraceId: 20765853-ab8c-46b7-8e85-c247799de7cd
            //Access-Control-Allow-Methods: POST,OPTIONS
            //Access-Control-Allow-Headers: Content-Type
            //Access-Control-Allow-Origin: https://my.kaspersky.com
            //Strict-Transport-Security: max-age=31536000
            //Date: Tue, 03 Jul 2018 12:30:31 GMT
            //Content-Length: 20
            //--JSON: {"Status":"Success"}

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
            //OPTIONS https://hq.uis.kaspersky.com/v3/logon/complete_active HTTP/1.1
            //Host: hq.uis.kaspersky.com
            //Connection: keep - alive
            //Access - Control - Request - Method: POST
            //Origin: https://my.kaspersky.com
            //User - Agent: Mozilla / 5.0(Windows NT 6.1) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 67.0.3396.99 Safari / 537.36
            //Access - Control - Request - Headers: content - type
            //Accept: */*
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7

            //Отправляем OPTIONS для третьей фазы авторизации
            userState.RequestHeaders = new WebHeaderCollection();
            userState.RequestHeaders.Add("Origin", "https://my.kaspersky.com");
            userState.RequestHeaders.Add("Access-Control-Request-Method", "POST");

            uri = new Uri("https://hq.uis.kaspersky.com/v3/logon/complete_active");
            userState = Navigate(uri, HttpRequestTypeEnum.OPTIONS, userState);
            if (userState.ResponseStatusCode != HttpStatusCode.OK)
                throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер вернул {userState.ResponseStatusCode}:{userState.ResponseStatus}");

            //Отвечает
            //HTTP/1.1 200 OK
            //Cache-Control: private
            //Server: nginx
            //X-UIS-TraceId: e89fec2c-6805-4eaf-a310-44c99318f288
            //Access-Control-Allow-Methods: POST,OPTIONS
            //Access-Control-Allow-Headers: Content-Type
            //Access-Control-Allow-Origin: https://my.kaspersky.com
            //Strict-Transport-Security: max-age=31536000
            //Date: Tue, 03 Jul 2018 12:30:31 GMT
            //Content-Length: 0

            //Проверяем на разрешение отправки POST
            if (userState.ResponseHeaders["Access-Control-Allow-Methods"] != null)
                if (!userState.ResponseHeaders["Access-Control-Allow-Methods"].ToLower().Contains("post"))
                    throw new Exception($"В ответ на OPTIONS {uri.AbsoluteUri} сервер не разрешил отправку POST");
            #endregion

            #region Запрос POST для третьей фазы

            //POST https://hq.uis.kaspersky.com/v3/logon/complete_active HTTP/1.1
            //Host: hq.uis.kaspersky.com
            //Connection: keep-alive
            //Content-Length: 217
            //Accept: */*
            //Origin: https://my.kaspersky.com
            //User - Agent: Mozilla / 5.0(Windows NT 6.1) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 67.0.3396.99 Safari / 537.36
            //Content - Type: application / json
            //Referer: https://my.kaspersky.com/
            //Accept - Encoding: gzip, deflate, br
            //Accept - Language: ru - RU,ru; q = 0.9,en - US; q = 0.8,en; q = 0.7
            //--JSON: { "logonContext":"onWbhMLrEZviwRJk7VR1iQ3hh1DaYq7JmpxeB5jUMbqaCTps_0CSAdHQOBnRYRu0GOLvEmBOFmsG3sqfR25ce4-IvPr-zG_RXUqPeLztx0tJEauYdic2L4qQCDjwEF6BcoyRLWaNiI3EJReA5GlHOVxddyvbgXH8cEgV9bXqA9o1","TokenType":"SamlDeflate"}

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

            //HTTP / 1.1 200 OK
            //Cache - Control: private
            //Content-Type: application/json; charset=utf-8
            //Server: nginx
            //X-UIS-TraceId: c51d45af-9f7c-4c53-b80a-35719c6ceaa1
            //Access-Control-Allow-Methods: POST,OPTIONS
            //Access-Control-Allow-Headers: Content-Type
            //Access-Control-Allow-Origin: https://my.kaspersky.com
            //Strict-Transport-Security: max-age=31536000
            //Date: Tue, 03 Jul 2018 12:30:31 GMT
            //Content-Length: 8270
            //--JSON: {"UserToken":"7XrH7txIk+erNHqOXIneCd0C6Ipk0RQ9Wbws6L13xXq1Oewj7SssJbXafo3pGQz2snv4mwxmuIzI4C8Q/N///r9+WudtWT9Z2bRly2pnyTZX6+kMTdZb2TIO/ZJxQ9tmyVoN/Q+vru2XT19Zfv6xXNfxEwimQ7J8HKKlWj4MY9Z/HOYCPJYPy+vL769bQQSCcBj58fM/UPZ9j1rl2Vp11/pYtk/cnEVrlv6i/6L8B9qXLyoxEILBb4++rCD4w/Xgy88vej9sa9V++Qt/hD6+lvTHzwgEUx8g8gOEOjDyCYU+ofBHkkbCn8DfGfHNIuE1VnO2/F+zCPtXFv1ixOefwL8e2viJGce2yhZn+NXI8Vcjl6TMumj5eD1Zhmj8xchfbKTBcWir5Pzxi5zok9Cn41D1q5Xl2Zz1SfarvOhXecdxfDzQr2K+BBuEKDBK08u2peqLX+Qw3wifv7AsF0+S9Ws2f2yiZczmpTk/JkMHfnHst63fVn8x4Cv9dw7+KbGy9A+p9fknoU/mc7zozHXU82+Z/POP29x/+hqQT33UZVdqJ59sRlM/IR+hT9H3zZf9r0vtp1/F8NEa/eCcY/Y3/sPgdYyXhovp34Q26y5Hf/zl0L4I+gdcf1J5GaFlazmkPzBtMVyeld0/EBJlC4ITH5I4+fEH8PNPSnbKfT58d/1fskNfon9tSJeq+GJE9pvTF/svPvwTt3/P+1+zfl6iD0OUjR+6IofHSx5fFVdw/6GkPziylBH89QjAv9r067l8/mn49KeS9Md8H/5brvj1T/Zaf7vhPwU4RH9JqG//ycuyZbOdzVXU/p6iX+n5mdN/Vr7flh/UKP7B5Ywfrr17dRnJfUnWvEqir/nNbJdzXxT+jx947ufoWl1JWCXfCb9euq+r6+L9BP5J11fd3+zQty7O5s8wRkEoTdEwRBAYiWE0hNH4tfzG+oetv5f23RfwN0fBvzvp69Gv0cg+cdVYZvM3lu8rL2q37PMZhqerki1KJQGpSStE7oABJFvDPGCk761qgbMeAJ+PoOjF6nyfWGUt63OmluNoIyvyG0PpZKR6ua7GXbHDdqLjR6goynQPil2EnKE+VA4y8GrjlMAIx2gxNcM00OnMlQWqOooTOgB+RpCBGkGI6gPCJt4Av/KNHJW0WwYNeMZxw/SGBMrHa7aKY44UncK8F+oKuMxq7MvuVWYJbnu6R+4RNma6Q/AqSEqgDvLQrXjItE8mBWDSF2NhV5qQmqKV2TCGfElz6+8rFkmVZEeyiPahMHZ7m7QNyAZmgHaPwt9S1nCxuVZjaSAMWQMYU3UXVgUkIfZri2CRMvFq+nAqSEcfI3T8/POXC/L7c/5t/Uvg/lgPfh+ur+Xq93t/R/gmDGJXzHBv2wP0M0lk4RWEs/cW3+q+w9H3nlWhy7evWXfkJ0xgsKKtATJPE5ABqbZ4bkC9DEgkMgORI30yoq6/DQCyFCIX8BGRw9p+y3lvOwUZRUrLe91WT5ZxENRBkyX83NZrd2469G36KS/FZ7nxs4t0yj4/vMIqVjA/b3p6H7HAhrniUPhDljUz0k7SrHRji8lVzmEsgI2uy3TtQRjBXeAQcCrOXW7PfOZY32WWUY+2rttFHbobCfs0+YxPB+OOoq+hsNpEwN7ryRs13GEgjj00yMPkF15G8qOOTVO40UEstzdMojzMM7s05+9KJIew1FfK2ULpRuRYEo8bOwR1PjxePljUfL2pdbh1QNaItQR4u6rtTVVuFUxxKIhIwhvWWtJLuPecWy2IgASrJanymshzlsog8fXBdZjpEDLq7qIyN5ed1yZ6ucuIhauzuEtQ77IBebiT9WiiiL3DAKMGWS4+Yh8gclttBv4+xfhgH8Uu3PF6RqbYmAyQ3zAbqR++VgELSseMMNQgcqV3zwI8H6iv6dX2b7k1PBmPG8Shcqiv1aQzSyvbEeNOXVhmdoKg2o/t2M6avJWQVNPdKIt6ZD1N32sHSWOkMdf40y+eIZk94FtgqVsRwxF536KRbWbvDYKBh3q22+NONd/oUJZ36w1ZM32go+/3EbvdnBsJIJJ58DTMv2Y5G9EHMZX3YYQ68dlZDMARc669mfZeFqEqbDAQUTpEnEwLtChDKLtdG54u4Agcg+8jChIGJmOPE+HxRblxpTTWJEIDD4pMIbm5EB6jdhoBl9emo25Eaw4IbJE96gjyuqoLOCH3DUIeQIbeBY2NayT0PDzp4RzJHeI8KVFg7PEBP2Ffr8zONF/hjIDhpL+HbNBznAIAtOpb1DJH3mwGTq4pcjhOPt7WDse0V0FRzr089QPhD6eHaK2lnNbC8Umi/dMAz4x9J8VO1eMtzp/V63AEns8n2XDeGF45k9U/q61C16hTfLVfp7dF48sNatuWep4Ma/GydXtvhBKulaTw5BGmh2DQIEq+sxcFrAocvpvFLLRFdEsgbHVaxBJKd7oWQu6g9e5GZyqUAdVYVgr86HTbPngeua/OZ3J/kVEUPEQLZs2o20JJn0e3vUVM99IOB4DeLTjt1rz04z1WfNug5C0QlGzxxpcUs+CmkUPyjjdCsElxuT+tpwpFe6Q2sfdOe9rSCd2KBzWPPCuyXCLWG66cOFFGpYjhSeU13jW3m9Kp5f0u7LPTBudej5ZuLkcX7UZdtReESuVeKWoPMB6IMjTw48zKAphypL35LDrYxVAJ1It6qZWASlpDpY8OqRZXkPtXL7NS6FAP7L6atFPQg4Uhg2oqef3gD3O67YDikm1PIDTMRZgg0Wub7m9VAsC8k+gbrKlIO4QZnspuTFqZZpp0Nte0jJAcTfiTvu/yqwV9yn8/Vjp3ZYWLOSCBdxEdjR1VeIIgEpVyVL0G5AKA8Sm0lzTpiVWYiuUxBLsGzO19gD3a4716Ppf4rXd8++5ubv1CJRtIljto38G3f8B3wJXnejf41L0jEfx+8u7mklAjy2T9HGzKy2Xag4Z1iPjT4hndsygkUmQVGwfz3UMt6WDL2cMaa3X3ldxsSykh/tkmECOe0vzgXq7lKPaEcDwwSvMNvTs+Y2r78dikQlSJmDpnndWyBqVpJQHDF18/p1qV+/sS0o7n3lXiTmUNNj4DEKPTJ9Sl3UmKTEw/yWm57vOYIi/SdsodOnYpXEdt6iS3vmHFCLoTOPVJQ0o2hzCi06mneqPsqOriaurdI8/CLt9p46ULDd7riHb5RTHD9jzLlm8XMNsUF8ZVyc4LxsdBu6/3moHHFU1wL5UZ/V2d/e6+e9jw4fZJOJzUjBtVbfQN7cCwfsG8N3nIjaTQOB8Mq8dukM9BVSA+5ZTLno3w6p7Nvgi20gSAOjwkH+uDRaw0Bs/NXLz1i3Hjb8g9XllFqcxxvd18xoacpcnf+u0AqpocAX9NiYoQN8lL9N0oiSstRrTye7xU1kQceZWgO2+Ch+6S8wJUgmLWR3rB3eGePAp3VHv8UPAi7Hk8HLZcbIg1URpOeWDi4hQtyT8Vh0f0d67dGuShFGsmZX7Ub4UQK5v/JJRhlvsCb/2n96xv/LDPbbeGihWIBkUzi//mArWIMwDObbB/9yHfEGYynmHp8ji5rAhkkwQi1UooTYjAhyW5gdqShMqIsHfEhGHnuhFaqkrLet40qXh798cx2FNfvu5rS+4gvfBVCoP1JN2VLmq8RekWpOfQl8d4mF2VbJoQLqIjZqJwyrmRVo33isrPL/DMKWgt8WovxZUTgojBdzdmnq+OCUBcvIfA+xDzpKCg9+GYxCMPXJHqRSWi4GfyTIeG6APHOneJJ7ezqGy1zxvF2CyyrKEeDmvNfdOVR/gsNIDU6R2A4WnYFZL9ARcIU5v2rWFJ59QAkT01VBRGhF+UCM7Rp+4/ys0U48ACMJI+HpCOm6WAloCKEZtr3GviKaaub6CNLFZAbAaQsm0BPhUkwQpIOb/QRLhByZ7NQFm8HcA8r9oq+15JS62mn/5Ddl9zX+iPh0JgmBa91IFPSd6jDgA17wja2WTxAJolIiM90NWzOxQ05NlCfb3ReLkVnVC7LNSEq3eeQHAiuxfcVNR4doUsO75QgWdtjJluhFRrVUgNshN039Kylelti+H8ea+c7Ng0+769Ox4r8v1dgEXZQXEBPK8bl7igREevtWeTsjFpu0XX0p9SYdFkHLK5euOMkCdnb9zEPpDSNTP693oh7OrBAWRfu5IQuaHigER8HvlLf2NvBRORlvHeFvq4l8x9up0oDMJEDL0tqtkO3hhJkgrLDVXiaEKCMkOpNbpxMDBhCq2/7TbLGHA6mAeUAG7nsRLQAoVivpbXsuhZlqIvAG41LhGyiNwaNwl2myMZA8Bg0SpvAUEBD7pVqZqm5vaqsRviB02ZK4olzli2YPe9yQoxuGGxQ5T8iy+tWZJ0201hZAIUgO2otlDHo7yQm2vw+VVn17QF5St6WZgl4e6LZC+7My66vUko6GvhRmZB3e0mW291nxkt9wbr7rSI9xJdu9sDTKoz+pBGlz3a8/UsLPzIsToSAswjFPCMM3KQSPu9FsXV5zUIm92DyDioLJWOzLwp0ngqYunftb71Hk+8qB2zsq+QhqAt+0V/j5Oru7Pac0wpuG3Nmhm5TRuYaX6wsUdxfdDasULWO4zI3JHN5yM7J0nXzHunIQufTTZ5T4fHY2fJM1rQ29u+WUFW3EPSrq3UvHouje+TFxIcxwN8UhT2EFIERe2zJW2kEbYIJ9q9G5dgazLwehWeXLC+VOXGBT40HPsqtiOgWTCFr81V4nqtueOD15kQGl0ZNNPa84IUKmtU9tvn6PweLkIJHOGjhqEGZq+m7obebPrtoRCDkx2Aw+eNUNoYsS6oXPRj4mPj46jrNT9yZTOxBEGhUpxE8E09TVd5Zl3RahYCG9tkHfZRiRw5q76/nI15N/iACq6u4Cbc5aY/SpIfagltSfN1N55xcyGJR6rl7hH59xoNg9aROQl/wENghhVWOX5ga/BViX0V8brlNT8BOnY4R7iOmyseb59/JkWCpZo8yYNwaLZ1AGp051ZlKlwzddeXyUGbOWRjUo05AbmMOGo597jgvjuTz1UrOdc7kwZakteLKGrliKWwebwnKGw1deup4Bnx8lLrRqfcAN7M5Sz2M+sly/M9OfKirsFKq06sVx87t3YGiQZCdAI3RieI2B+irKz3vPNoCGj0UDJbgPRbfyWeMW+CWD6Oq1xKmqyY4PXGDBLghsj3GvDQqNn7uT97F771xB7YsWSiujwoqWdA4ZQ+M9QLG0vvHO16T4Ox2tnHtKUBDldzzGpScyKBtMJYPaDylbihI1xoGTqvvjDGvCgGcxW73jrRMSDiwpVkGTuaZ7B9xdKlDfmQC1iXhVYWui1bdAVZwAJ+X5uwnkBYajm+JECn1AiHWKibBnLDzuZxiTFv7bxhnFivSAQKxsIZIuEG6SSy0Szg55DcZGDEyd1N48CJTH+FpecRovINTRv/3hroO0YlLNGP66hKtfcRiuM4sNEWeEDwVwQ8bQLRMrVuDB9dcdi94XV5+oQ0vovoTZQCYagJWvfUEFvpm8dfVl1iF3jjBlwo5bf68PBiaMpxDC+/RdjMIAxxUR7zvPT9zJtNMeKY4gk0oi+E/S7KnZOe2NV8wiL/SAYiZUNGj13gNoR7iT5i0skhP+4YjlcYHpeR3PbPuzQxUZgutV+zyrYy6bLwWy2Ed0FQ3nreXBiFftF3l86FOzM1C4WH/vnAjRsA5AAzQIatsyH7Qqr43TtZ7GmrxdNDR8Dohaq2bcoz9MJT4kQteWkE1JkjpV+8fI133YVmTpbX7Pd7opk4zFOctgsRk8iC3nP/giQ+0GLwSvY7EG1Qy0HN0UkOGtlIDs/eg3ouYZAeDsbsOAxIJLBw+Ry9u/pRiwjsvnhYlyZUQ0A39Rrb4bhTDlrVoV1jErrGZ2ssVcze8Fz5vJUebZLc82k/n8iYxyPTlvCaRqCLF3DMVNFedPjNKjvz8I+USd8oc0Dp/QqQqkhEJzxajBpuL9lEptfbfGJeCDidwVgcVOB0QG12AiOHFDi6IZk7Ttp6qrN2K0KzjFJ2EzcCbyhvS8yY4Hr7IlwOGuixjDLnsI4Cu7V1YRCNE7enmbOICoEqwNhiGLcVa49Xm0qr1kzqpgs9PBAI5GjKneiCcYD0uGkdfwxVGvn4Ou7awzT9W16nhF4YG7uuj9c9TBtJ2QTgnJMTV7joOWZjvHSspzeFC8RuC7HLgEJXfzqArCTz3Cz67GzQQLPfXqnOZOc9Alukwgrw0WbIDqMw2sbm6bjowtxM/+nUhnhgWB5G+VMbJp8MbuiiJne2H3AJZSKIXw+XMEB6Z4hFhC75cQNtfgczcL8amQp7x4VckWkqdbBfE7bpqMm6n3uBajfvPZ7a8RSS2jxPliCi9xxFYUXGWy2+1rluG/x1pGIjhYCS5dvOaTQtROIczNvCZE/pSVRJybFr/rgQtpBBD3+cYTjnwtRHBGjU/TSao6tHMHBrBQMQldT3PGPlzFfRRNf3zkD9FNHekOLiDO4gAhhr3q0ZqFGgoYGlPXIj12KuZFnqB5ZtWwvEBwuNMt5+GdCtdTCFV5r6xT8YDG02b4MOpcDEjkNHTzTa3a0MEtHTmxdgDGVc8HU5bhO71Lz5AHM5HmGPxVhnQGOhzAue9IAqgK9mfqDAGU/Juu9yXigJ7e5WSeb6SOfe1jtug+PLNvncF7LsDi9ezna4K/D0SkX9JDOZ/egFII6R8TAs1VlNZpc6IjGMo96iaS1XXhy3VC3NZ+abkyTkcUpClSFflQU+Qxl13qSN+/pdncK0sMGYadYMwiGw4WDH4fNxaiOzhvEuc8/oCRpk0uYtCikMjfhudapamJbEtCuiX3pfmqz3bBF8oU35PU80j+ruB23jbbA+MRnxYtl1oaf/3HracEPh0a9KcYLhmDteQK5y0FBZfvq3VBTik25G1dknc3k8qA2pKKZ20sos9PBG6oqak+8bby15AvvQZHX5O19bACOiB52OgnvVpon0iTNJ+kldRGJhF9XqjoDu7pHAnNk4Nh5vS/osuugFgVLFGuciVHx4Nu+i0xA15HuVIKhgGcVrWYLzyEzmLdmrkHuKNXcIiGVKy2PdTZKGau/h3U8o7HBH3TQWkMbXsQQzhTy0kRtV3JoUoNujHsom5+W8suBhZRMfPTxdoiVhbaeE7Zz0MeIzbztmayODI4wTPYtqT2DGbKkF5A0tSrSjwhvT1F5VzBJFgQO2nVIjPHjP4gmACuBEUs+8VIvtXopFi4J6DhoHPUboSlc3NtB04Lk1dglA8uMrAAz4BO5AeZvw6uqGpmKpstc7NENGrO6GftoYYisojppX/G29dwGYYQsItJJms7R1dPsawHHqNdse/OYWnYJL+OBq2ktDrIYr4mw7IYX4GHtKG5esh8CoO9gU6Wt8VIf3zJ5gWhyqKD0bqJ2Iub47kkdvkgsU05RMmLvw8co8S6dc+J4K3w/LOEYGD9/qy84kFYT0zmaMEaO94X4BlrPQyeh2Dk1FUS5iAKCNyz1k5aOuD0Abnbz6fCiYgfm+4ssea2n7zUDEFF4SL4AWrsVc3Efl8EEOrIwwu5ZE4/Cw1NEIQohI+KLRQcPiA33OadS/c5YFVC3/fOb8NB8R3jAQKy9eumPSHtJtbRQB+5Crq0LJep8UzwyDlkyMiEYYTV5dc9zGYoZHJ8bYw6QmTWrqjkLzdT8LlXuSoZBvBHYqTaRsG2bRszeVXKIZmMemY9UbytzIbdf9fEmVvr6wje0LVkSKvhaBxw4pzwFj7k1y83wWieDhVaPG8IxH4+7cvPhWURK0GFTPTVEilO8oMrMKO+whQGR62XiHRXEFN2U2Ca3QCA3ujL2kqWB8Sz1HLJ12cF/gmjZxyh9XoyEi3fDC3qRupHmbT9zWwSHceMqS6nYtWqTackc/lYL1dejyl1HJH0i/jF7+OoK+iH+dbP/6McDfDcP/9JRZ1ygps/R3M7i/mYKm2Ah/+kr7w+j774ah30hfpp9L1LUf1i+MH8Z5yKs2+wB/hP/ty6DdQz5C30fjX+T/J6T+q5kq/HWm+qcJ93/rtPbLvCv9MkrNq2z+4Wu0/vtOQ+Z//Pw/UZpKUTQjPkRpnn7AUhj6QF0d1ocsI7GEoBMMhvJvc7df7bhi/rfz1P843H/a4fbR/0+J/5dS4l8G/NueX0P7+R99KPNd9G9sf0yur6T/7Gdj4NfPCP5k9e+FX37/1wSzWTRn83fJ38X8SdPffKX2Dzb99t3c5/8D","TokenType":"SamlDeflate"}

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
            //POST https://my.kaspersky.com/SignIn/CompleteRestLogon HTTP/1.1
            //Host: my.kaspersky.com
            //Connection: keep - alive
            //Content - Length: 8928
            //Accept: */*
            //Origin: https://my.kaspersky.com
            //X-Requested-With: XMLHttpRequest
            //User-Agent: Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36
            //Content-Type: application/x-www-form-urlencoded; charset=UTF-8
            //Referer: https://my.kaspersky.com/
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7
            //Cookie: SessionID=3bwmzvgjicxusiyta2dbsjo2; _UserCulture=ru; __RequestVerificationToken=Ygokk26uqKvuVu6bQHL1w5CL0Opfwy6pauUunZv82o3VtNixxVPHAqN0Ry_OFSBXvLkK4UI1Z7noinmht5TG99ARH0eMyeAA120BpPNmBNA1
            //--JSON(корявый): samlDeflatedToken=7XrH7txIk%2BerNHqOXIneCd0C6Ipk0RQ9Wbws6L13xXq1Oewj7SssJbXafo3pGQz2snv4mwxmuIzI4C8Q%2FN%2F%2F%2Fr9%2BWudtWT9Z2bRly2pnyTZX6%2BkMTdZb2TIO%2FZJxQ9tmyVoN%2FQ%2Bvru2XT19Zfv6xXNfxEwimQ7J8HKKlWj4MY9Z%2FHOYCPJYPy%2BvL769bQQSCcBj58fM%2FUPZ9j1rl2Vp11%2FpYtk%2FcnEVrlv6i%2F6L8B9qXLyoxEILBb4%2B%2BrCD4w%2FXgy88vej9sa9V%2B%2BQt%2FhD6%2BlvTHzwgEUx8g8gOEOjDyCYU%2BofBHkkbCn8DfGfHNIuE1VnO2%2FF%2BzCPtXFv1ixOefwL8e2viJGce2yhZn%2BNXI8Vcjl6TMumj5eD1Zhmj8xchfbKTBcWir5Pzxi5zok9Cn41D1q5Xl2Zz1SfarvOhXecdxfDzQr2K%2BBBuEKDBK08u2peqLX%2BQw3wifv7AsF0%2BS9Ws2f2yiZczmpTk%2FJkMHfnHst63fVn8x4Cv9dw7%2BKbGy9A%2Bp9fknoU%2Fmc7zozHXU82%2BZ%2FPOP29x%2F%2BhqQT33UZVdqJ59sRlM%2FIR%2BhT9H3zZf9r0vtp1%2FF8NEa%2FeCcY%2FY3%2FsPgdYyXhovp34Q26y5Hf%2Fzl0L4I%2Bgdcf1J5GaFlazmkPzBtMVyeld0%2FEBJlC4ITH5I4%2BfEH8PNPSnbKfT58d%2F1fskNfon9tSJeq%2BGJE9pvTF%2FsvPvwTt3%2FP%2B1%2Bzfl6iD0OUjR%2B6IofHSx5fFVdw%2F6GkPziylBH89QjAv9r067l8%2Fmn49KeS9Md8H%2F5brvj1T%2FZaf7vhPwU4RH9JqG%2F%2FycuyZbOdzVXU%2Fp6iX%2Bn5mdN%2FVr7flh%2FUKP7B5Ywfrr17dRnJfUnWvEqir%2FnNbJdzXxT%2Bjx947ufoWl1JWCXfCb9euq%2Br6%2BL9BP5J11fd3%2BzQty7O5s8wRkEoTdEwRBAYiWE0hNH4tfzG%2Boetv5f23RfwN0fBvzvp69Gv0cg%2BcdVYZvM3lu8rL2q37PMZhqerki1KJQGpSStE7oABJFvDPGCk761qgbMeAJ%2BPoOjF6nyfWGUt63OmluNoIyvyG0PpZKR6ua7GXbHDdqLjR6goynQPil2EnKE%2BVA4y8GrjlMAIx2gxNcM00OnMlQWqOooTOgB%2BRpCBGkGI6gPCJt4Av%2FKNHJW0WwYNeMZxw%2FSGBMrHa7aKY44UncK8F%2BoKuMxq7MvuVWYJbnu6R%2B4RNma6Q%2FAqSEqgDvLQrXjItE8mBWDSF2NhV5qQmqKV2TCGfElz6%2B8rFkmVZEeyiPahMHZ7m7QNyAZmgHaPwt9S1nCxuVZjaSAMWQMYU3UXVgUkIfZri2CRMvFq%2BnAqSEcfI3T8%2FPOXC%2FL7c%2F5t%2FUvg%2FlgPfh%2Bur%2BXq93t%2FR%2FgmDGJXzHBv2wP0M0lk4RWEs%2FcW3%2Bq%2Bw9H3nlWhy7evWXfkJ0xgsKKtATJPE5ABqbZ4bkC9DEgkMgORI30yoq6%2FDQCyFCIX8BGRw9p%2By3lvOwUZRUrLe91WT5ZxENRBkyX83NZrd2469G36KS%2FFZ7nxs4t0yj4%2FvMIqVjA%2Fb3p6H7HAhrniUPhDljUz0k7SrHRji8lVzmEsgI2uy3TtQRjBXeAQcCrOXW7PfOZY32WWUY%2B2rttFHbobCfs0%2BYxPB%2BOOoq%2BhsNpEwN7ryRs13GEgjj00yMPkF15G8qOOTVO40UEstzdMojzMM7s05%2B9KJIew1FfK2ULpRuRYEo8bOwR1PjxePljUfL2pdbh1QNaItQR4u6rtTVVuFUxxKIhIwhvWWtJLuPecWy2IgASrJanymshzlsog8fXBdZjpEDLq7qIyN5ed1yZ6ucuIhauzuEtQ77IBebiT9WiiiL3DAKMGWS4%2BYh8gclttBv4%2BxfhgH8Uu3PF6RqbYmAyQ3zAbqR%2B%2BVgELSseMMNQgcqV3zwI8H6iv6dX2b7k1PBmPG8Shcqiv1aQzSyvbEeNOXVhmdoKg2o%2Ft2M6avJWQVNPdKIt6ZD1N32sHSWOkMdf40y%2BeIZk94FtgqVsRwxF536KRbWbvDYKBh3q22%2BNONd%2FoUJZ36w1ZM32go%2B%2F3EbvdnBsJIJJ58DTMv2Y5G9EHMZX3YYQ68dlZDMARc669mfZeFqEqbDAQUTpEnEwLtChDKLtdG54u4Agcg%2B8jChIGJmOPE%2BHxRblxpTTWJEIDD4pMIbm5EB6jdhoBl9emo25Eaw4IbJE96gjyuqoLOCH3DUIeQIbeBY2NayT0PDzp4RzJHeI8KVFg7PEBP2Ffr8zONF%2FhjIDhpL%2BHbNBznAIAtOpb1DJH3mwGTq4pcjhOPt7WDse0V0FRzr089QPhD6eHaK2lnNbC8Umi%2FdMAz4x9J8VO1eMtzp%2FV63AEns8n2XDeGF45k9U%2Fq61C16hTfLVfp7dF48sNatuWep4Ma%2FGydXtvhBKulaTw5BGmh2DQIEq%2BsxcFrAocvpvFLLRFdEsgbHVaxBJKd7oWQu6g9e5GZyqUAdVYVgr86HTbPngeua%2FOZ3J%2FkVEUPEQLZs2o20JJn0e3vUVM99IOB4DeLTjt1rz04z1WfNug5C0QlGzxxpcUs%2BCmkUPyjjdCsElxuT%2BtpwpFe6Q2sfdOe9rSCd2KBzWPPCuyXCLWG66cOFFGpYjhSeU13jW3m9Kp5f0u7LPTBudej5ZuLkcX7UZdtReESuVeKWoPMB6IMjTw48zKAphypL35LDrYxVAJ1It6qZWASlpDpY8OqRZXkPtXL7NS6FAP7L6atFPQg4Uhg2oqef3gD3O67YDikm1PIDTMRZgg0Wub7m9VAsC8k%2BgbrKlIO4QZnspuTFqZZpp0Nte0jJAcTfiTvu%2FyqwV9yn8%2FVjp3ZYWLOSCBdxEdjR1VeIIgEpVyVL0G5AKA8Sm0lzTpiVWYiuUxBLsGzO19gD3a4716Ppf4rXd8%2B%2B5ubv1CJRtIljto38G3f8B3wJXnejf41L0jEfx%2B8u7mklAjy2T9HGzKy2Xag4Z1iPjT4hndsygkUmQVGwfz3UMt6WDL2cMaa3X3ldxsSykh%2FtkmECOe0vzgXq7lKPaEcDwwSvMNvTs%2BY2r78dikQlSJmDpnndWyBqVpJQHDF18%2Fp1qV%2B%2FsS0o7n3lXiTmUNNj4DEKPTJ9Sl3UmKTEw%2FyWm57vOYIi%2FSdsodOnYpXEdt6iS3vmHFCLoTOPVJQ0o2hzCi06mneqPsqOriaurdI8%2FCLt9p46ULDd7riHb5RTHD9jzLlm8XMNsUF8ZVyc4LxsdBu6%2F3moHHFU1wL5UZ%2FV2d%2Fe6%2Be9jw4fZJOJzUjBtVbfQN7cCwfsG8N3nIjaTQOB8Mq8dukM9BVSA%2B5ZTLno3w6p7Nvgi20gSAOjwkH%2BuDRaw0Bs%2FNXLz1i3Hjb8g9XllFqcxxvd18xoacpcnf%2Bu0AqpocAX9NiYoQN8lL9N0oiSstRrTye7xU1kQceZWgO2%2BCh%2B6S8wJUgmLWR3rB3eGePAp3VHv8UPAi7Hk8HLZcbIg1URpOeWDi4hQtyT8Vh0f0d67dGuShFGsmZX7Ub4UQK5v%2FJJRhlvsCb%2F2n96xv%2FLDPbbeGihWIBkUzi%2F%2FmArWIMwDObbB%2F9yHfEGYynmHp8ji5rAhkkwQi1UooTYjAhyW5gdqShMqIsHfEhGHnuhFaqkrLet40qXh798cx2FNfvu5rS%2B4gvfBVCoP1JN2VLmq8RekWpOfQl8d4mF2VbJoQLqIjZqJwyrmRVo33isrPL%2FDMKWgt8WovxZUTgojBdzdmnq%2BOCUBcvIfA%2BxDzpKCg9%2BGYxCMPXJHqRSWi4GfyTIeG6APHOneJJ7ezqGy1zxvF2CyyrKEeDmvNfdOVR%2FgsNIDU6R2A4WnYFZL9ARcIU5v2rWFJ59QAkT01VBRGhF%2BUCM7Rp%2B4%2Fys0U48ACMJI%2BHpCOm6WAloCKEZtr3GviKaaub6CNLFZAbAaQsm0BPhUkwQpIOb%2FQRLhByZ7NQFm8HcA8r9oq%2B15JS62mn%2F5Ddl9zX%2BiPh0JgmBa91IFPSd6jDgA17wja2WTxAJolIiM90NWzOxQ05NlCfb3ReLkVnVC7LNSEq3eeQHAiuxfcVNR4doUsO75QgWdtjJluhFRrVUgNshN039Kylelti%2BH8ea%2Bc7Ng0%2B769Ox4r8v1dgEXZQXEBPK8bl7igREevtWeTsjFpu0XX0p9SYdFkHLK5euOMkCdnb9zEPpDSNTP693oh7OrBAWRfu5IQuaHigER8HvlLf2NvBRORlvHeFvq4l8x9up0oDMJEDL0tqtkO3hhJkgrLDVXiaEKCMkOpNbpxMDBhCq2%2F7TbLGHA6mAeUAG7nsRLQAoVivpbXsuhZlqIvAG41LhGyiNwaNwl2myMZA8Bg0SpvAUEBD7pVqZqm5vaqsRviB02ZK4olzli2YPe9yQoxuGGxQ5T8iy%2BtWZJ0201hZAIUgO2otlDHo7yQm2vw%2BVVn17QF5St6WZgl4e6LZC%2B7My66vUko6GvhRmZB3e0mW291nxkt9wbr7rSI9xJdu9sDTKoz%2BpBGlz3a8%2FUsLPzIsToSAswjFPCMM3KQSPu9FsXV5zUIm92DyDioLJWOzLwp0ngqYunftb71Hk%2B8qB2zsq%2BQhqAt%2B0V%2Fj5Oru7Pac0wpuG3Nmhm5TRuYaX6wsUdxfdDasULWO4zI3JHN5yM7J0nXzHunIQufTTZ5T4fHY2fJM1rQ29u%2BWUFW3EPSrq3UvHouje%2BTFxIcxwN8UhT2EFIERe2zJW2kEbYIJ9q9G5dgazLwehWeXLC%2BVOXGBT40HPsqtiOgWTCFr81V4nqtueOD15kQGl0ZNNPa84IUKmtU9tvn6PweLkIJHOGjhqEGZq%2Bm7obebPrtoRCDkx2Aw%2BeNUNoYsS6oXPRj4mPj46jrNT9yZTOxBEGhUpxE8E09TVd5Zl3RahYCG9tkHfZRiRw5q76%2FnI15N%2FiACq6u4Cbc5aY%2FSpIfagltSfN1N55xcyGJR6rl7hH59xoNg9aROQl%2FwENghhVWOX5ga%2FBViX0V8brlNT8BOnY4R7iOmyseb59%2FJkWCpZo8yYNwaLZ1AGp051ZlKlwzddeXyUGbOWRjUo05AbmMOGo597jgvjuTz1UrOdc7kwZakteLKGrliKWwebwnKGw1deup4Bnx8lLrRqfcAN7M5Sz2M%2Bsly%2FM9OfKirsFKq06sVx87t3YGiQZCdAI3RieI2B%2BirKz3vPNoCGj0UDJbgPRbfyWeMW%2BCWD6Oq1xKmqyY4PXGDBLghsj3GvDQqNn7uT97F771xB7YsWSiujwoqWdA4ZQ%2BM9QLG0vvHO16T4Ox2tnHtKUBDldzzGpScyKBtMJYPaDylbihI1xoGTqvvjDGvCgGcxW73jrRMSDiwpVkGTuaZ7B9xdKlDfmQC1iXhVYWui1bdAVZwAJ%2BX5uwnkBYajm%2BJECn1AiHWKibBnLDzuZxiTFv7bxhnFivSAQKxsIZIuEG6SSy0Szg55DcZGDEyd1N48CJTH%2BFpecRovINTRv%2F3hroO0YlLNGP66hKtfcRiuM4sNEWeEDwVwQ8bQLRMrVuDB9dcdi94XV5%2BoQ0vovoTZQCYagJWvfUEFvpm8dfVl1iF3jjBlwo5bf68PBiaMpxDC%2B%2FRdjMIAxxUR7zvPT9zJtNMeKY4gk0oi%2BE%2FS7KnZOe2NV8wiL%2FSAYiZUNGj13gNoR7iT5i0skhP%2B4YjlcYHpeR3PbPuzQxUZgutV%2BzyrYy6bLwWy2Ed0FQ3nreXBiFftF3l86FOzM1C4WH%2FvnAjRsA5AAzQIatsyH7Qqr43TtZ7GmrxdNDR8Dohaq2bcoz9MJT4kQteWkE1JkjpV%2B8fI133YVmTpbX7Pd7opk4zFOctgsRk8iC3nP%2FgiQ%2B0GLwSvY7EG1Qy0HN0UkOGtlIDs%2Feg3ouYZAeDsbsOAxIJLBw%2BRy9u%2FpRiwjsvnhYlyZUQ0A39Rrb4bhTDlrVoV1jErrGZ2ssVcze8Fz5vJUebZLc82k%2Fn8iYxyPTlvCaRqCLF3DMVNFedPjNKjvz8I%2BUSd8oc0Dp%2FQqQqkhEJzxajBpuL9lEptfbfGJeCDidwVgcVOB0QG12AiOHFDi6IZk7Ttp6qrN2K0KzjFJ2EzcCbyhvS8yY4Hr7IlwOGuixjDLnsI4Cu7V1YRCNE7enmbOICoEqwNhiGLcVa49Xm0qr1kzqpgs9PBAI5GjKneiCcYD0uGkdfwxVGvn4Ou7awzT9W16nhF4YG7uuj9c9TBtJ2QTgnJMTV7joOWZjvHSspzeFC8RuC7HLgEJXfzqArCTz3Cz67GzQQLPfXqnOZOc9Alukwgrw0WbIDqMw2sbm6bjowtxM%2F%2BnUhnhgWB5G%2BVMbJp8MbuiiJne2H3AJZSKIXw%2BXMEB6Z4hFhC75cQNtfgczcL8amQp7x4VckWkqdbBfE7bpqMm6n3uBajfvPZ7a8RSS2jxPliCi9xxFYUXGWy2%2B1rluG%2Fx1pGIjhYCS5dvOaTQtROIczNvCZE%2FpSVRJybFr%2FrgQtpBBD3%2BcYTjnwtRHBGjU%2FTSao6tHMHBrBQMQldT3PGPlzFfRRNf3zkD9FNHekOLiDO4gAhhr3q0ZqFGgoYGlPXIj12KuZFnqB5ZtWwvEBwuNMt5%2BGdCtdTCFV5r6xT8YDG02b4MOpcDEjkNHTzTa3a0MEtHTmxdgDGVc8HU5bhO71Lz5AHM5HmGPxVhnQGOhzAue9IAqgK9mfqDAGU%2FJuu9yXigJ7e5WSeb6SOfe1jtug%2BPLNvncF7LsDi9ezna4K%2FD0SkX9JDOZ%2FegFII6R8TAs1VlNZpc6IjGMo96iaS1XXhy3VC3NZ%2BabkyTkcUpClSFflQU%2BQxl13qSN%2B%2FpdncK0sMGYadYMwiGw4WDH4fNxaiOzhvEuc8%2FoCRpk0uYtCikMjfhudapamJbEtCuiX3pfmqz3bBF8oU35PU80j%2BruB23jbbA%2BMRnxYtl1oaf%2F3HracEPh0a9KcYLhmDteQK5y0FBZfvq3VBTik25G1dknc3k8qA2pKKZ20sos9PBG6oqak%2B8bby15AvvQZHX5O19bACOiB52OgnvVpon0iTNJ%2BkldRGJhF9XqjoDu7pHAnNk4Nh5vS%2FosuugFgVLFGuciVHx4Nu%2Bi0xA15HuVIKhgGcVrWYLzyEzmLdmrkHuKNXcIiGVKy2PdTZKGau%2Fh3U8o7HBH3TQWkMbXsQQzhTy0kRtV3JoUoNujHsom5%2BW8suBhZRMfPTxdoiVhbaeE7Zz0MeIzbztmayODI4wTPYtqT2DGbKkF5A0tSrSjwhvT1F5VzBJFgQO2nVIjPHjP4gmACuBEUs%2B8VIvtXopFi4J6DhoHPUboSlc3NtB04Lk1dglA8uMrAAz4BO5AeZvw6uqGpmKpstc7NENGrO6GftoYYisojppX%2FG29dwGYYQsItJJms7R1dPsawHHqNdse%2FOYWnYJL%2BOBq2ktDrIYr4mw7IYX4GHtKG5esh8CoO9gU6Wt8VIf3zJ5gWhyqKD0bqJ2Iub47kkdvkgsU05RMmLvw8co8S6dc%2BJ4K3w%2FLOEYGD9%2Fqy84kFYT0zmaMEaO94X4BlrPQyeh2Dk1FUS5iAKCNyz1k5aOuD0Abnbz6fCiYgfm%2B4ssea2n7zUDEFF4SL4AWrsVc3Efl8EEOrIwwu5ZE4%2FCw1NEIQohI%2BKLRQcPiA33OadS%2Fc5YFVC3%2FfOb8NB8R3jAQKy9eumPSHtJtbRQB%2B5Crq0LJep8UzwyDlkyMiEYYTV5dc9zGYoZHJ8bYw6QmTWrqjkLzdT8LlXuSoZBvBHYqTaRsG2bRszeVXKIZmMemY9UbytzIbdf9fEmVvr6wje0LVkSKvhaBxw4pzwFj7k1y83wWieDhVaPG8IxH4%2B7cvPhWURK0GFTPTVEilO8oMrMKO%2BwhQGR62XiHRXEFN2U2Ca3QCA3ujL2kqWB8Sz1HLJ12cF%2FgmjZxyh9XoyEi3fDC3qRupHmbT9zWwSHceMqS6nYtWqTackc%2FlYL1dejyl1HJH0i%2FjF7%2BOoK%2BiH%2BdbP%2F6McDfDcP%2F9JRZ1ygps%2FR3M7i%2FmYKm2Ah%2F%2Bkr7w%2Bj774ah30hfpp9L1LUf1i%2BMH8Z5yKs2%2BwB%2FhP%2Fty6DdQz5C30fjX%2BT%2FJ6T%2Bq5kq%2FHWm%2BqcJ93%2FrtPbLvCv9MkrNq2z%2B4Wu0%2FvtOQ%2BZ%2F%2FPw%2FUZpKUTQjPkRpnn7AUhj6QF0d1ocsI7GEoBMMhvJvc7df7bhi%2Frfz1P843H%2Fa4fbR%2F0%2BJ%2F5dS4l8G%2FNueX0P7%2BR99KPNd9G9sf0yur6T%2F7Gdj4NfPCP5k9e%2BFX37%2F1wSzWTRn83fJ38X8SdPffKX2Dzb99t3c5%2F8D&returnUrl=http%3A%2F%2Fmy.kaspersky.com%2F&resendActivationLink=false

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


            //HTTP/1.1 202 Accepted
            //Server: nginx
            //Date: Tue, 03 Jul 2018 12:30:33 GMT
            //Content-Type: application/json; charset=utf-8
            //Content-Length: 40
            //Connection: keep-alive
            //Cache-Control: no-cache, no-store, must-revalidate
            //Pragma: no-cache
            //Expires: -1
            //Set-Cookie: _UserCulture=ru; expires=Wed, 03-Jul-2019 12:30:32 GMT; path=/; secure; HttpOnly
            //X-Frame-Options: SAMEORIGIN
            //Set-Cookie: _UserCulture=ru; expires=Wed, 03-Jul-2019 12:30:32 GMT; path=/; secure; HttpOnly
            //Set-Cookie: MyKFedAuth=77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz48U2VjdXJpdHlDb250ZXh0VG9rZW4gcDE6SWQ9Il8yZTViN2U1OS1hNzVlLTQzNTctYjhjYy05NWQ1Y2MyODlhNDQtMEVCOUMzRDYwOURFNzQzRDdEMjc5ODlFQzE0QTBFQkIiIHhtbG5zOnAxPSJodHRwOi8vZG9jcy5vYXNpcy1vcGVuLm9yZy93c3MvMjAwNC8wMS9vYXNpcy0yMDA0MDEtd3NzLXdzc2VjdXJpdHktdXRpbGl0eS0xLjAueHNkIiB4bWxucz0iaHR0cDovL2RvY3Mub2FzaXMtb3Blbi5vcmcvd3Mtc3gvd3Mtc2VjdXJlY29udmVyc2F0aW9uLzIwMDUxMiI+PElkZW50aWZpZXI+dXJuOnV1aWQ6ZThiZTU3ZjMtZmI4YS00MDA0LWJjZmItNTU1ZjYzZDUzYjIwPC9JZGVudGlmaWVyPjwvU2VjdXJpdHlDb250ZXh0VG9rZW4+; domain=.kaspersky.com; path=/; secure; HttpOnly
            //X-Request-ID: 3a7b20ea43f6dcdad774f58d3fd431f1
            //X-XSS-Protection: 1; mode=block
            //--JSON: {"returnUrl":"http://my.kaspersky.com/"}

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

            //GET https://my.kaspersky.com/dashboard HTTP/1.1
            //Host: my.kaspersky.com
            //Connection: keep - alive
            //Upgrade - Insecure - Requests: 1
            //User - Agent: Mozilla / 5.0(Windows NT 6.1) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 67.0.3396.99 Safari / 537.36
            //Accept: text / html,application / xhtml + xml,application / xml; q = 0.9,image / webp,image / apng,*/*;q=0.8
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7
            //Cookie: SessionID=bqqreb0oyayegkgnmy2fa51x; __RequestVerificationToken=xo8mQSKtjOrY1oCWz1TjbIg7ljk7kw1ixykFHZP2jxaXhf7GwXYywVmDGpwLxYUV3PX756ibn5ldzUSylQyo0wM-Umui09v6cLVQT8sUJUs1; MyKFedAuth=77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz48U2VjdXJpdHlDb250ZXh0VG9rZW4gcDE6SWQ9Il84OGM0MDBiYy0yOGVjLTQwMDUtOGQ4OC00OGExMTM3MGIxYWUtNTVBMzAwN0VCM0EzNUEyRkUxRTQyREM2NEMzRjk1RkEiIHhtbG5zOnAxPSJodHRwOi8vZG9jcy5vYXNpcy1vcGVuLm9yZy93c3MvMjAwNC8wMS9vYXNpcy0yMDA0MDEtd3NzLXdzc2VjdXJpdHktdXRpbGl0eS0xLjAueHNkIiB4bWxucz0iaHR0cDovL2RvY3Mub2FzaXMtb3Blbi5vcmcvd3Mtc3gvd3Mtc2VjdXJlY29udmVyc2F0aW9uLzIwMDUxMiI+PElkZW50aWZpZXI+dXJuOnV1aWQ6N2I3MmEwMmMtMDY3Yy00NGE2LThmMGUtMTE5Mzk0ZDBkNjgyPC9JZGVudGlmaWVyPjwvU2VjdXJpdHlDb250ZXh0VG9rZW4+; _UserCulture=ru-RU

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

            //HTTP / 1.1 200 OK
            //Server: nginx
            //Date: Thu, 05 Jul 2018 10:41:05 GMT
            //Content - Type: text / html; charset = utf - 8
            //Connection: keep - alive
            //Vary: Accept - Encoding
            //Cache - Control: no - cache, no - store, must - revalidate
            //Pragma: no - cache
            //Expires: -1
            //X - Frame - Options: SAMEORIGIN
            //Set - Cookie: _UserCulture = ru - RU; expires = Fri, 05 - Jul - 2019 10:41:04 GMT; path =/; secure; HttpOnly
            //Set - Cookie: UniqueContextHash = 1jsR4w4CwJoqBi4R98JjUA ==; domain =.my.kaspersky.com; expires = Thu, 05 - Jul - 2018 11:11:04 GMT; path =/; secure; HttpOnly
            //X - Request - ID: 8258a64e9697bfb3c611d07d1093c4c0
            //Strict - Transport - Security: max - age = 31536000;
            //            X - XSS - Protection: 1; mode = block
            //X - Content - Type - Options: nosniff
            //Content - Length: 399887

            //Проверяем наличие UniqueContextHash в кукисах

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
            //POST https://my.kaspersky.com/MyAccountApi HTTP/1.1
            //Host: my.kaspersky.com
            //Connection: keep - alive
            //Content - Length: 2
            //Accept: application / json, text / plain, */*
            //Origin: https://my.kaspersky.com
            //X-Requested-With: XMLHttpRequest
            //User-Agent: Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36
            //content-type: application/json
            //Referer: https://my.kaspersky.com/dashboard
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7
            //Cookie: doNotTrack=1; _UserCulture=ru-RU; visid_incap_1639512=gfn3fnmtQ9COzMb36N48fANrO1sAAAAAQUIPAAAAAADqTzY2twPH0vR1LnK1BE5i; __RequestVerificationToken=VgHfgKOQt9SPXkKeoHntjIMXGaskIgFxKi91VklhXtiIL-K5i6Ec8maStQ3zexsAiGDgLKiZ-W9LC4UI4Ohd3jmCoEw_jw_U1EDDfcRd-Q01; SessionID=dqvotnfyq2eexfdlvxyvyqjg; MyKFedAuth=77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz48U2VjdXJpdHlDb250ZXh0VG9rZW4gcDE6SWQ9Il84OGM0MDBiYy0yOGVjLTQwMDUtOGQ4OC00OGExMTM3MGIxYWUtNDRERUM1MEQ5Mzg3QjQwNDg3RkY3NzY3N0VCNjM0MUUiIHhtbG5zOnAxPSJodHRwOi8vZG9jcy5vYXNpcy1vcGVuLm9yZy93c3MvMjAwNC8wMS9vYXNpcy0yMDA0MDEtd3NzLXdzc2VjdXJpdHktdXRpbGl0eS0xLjAueHNkIiB4bWxucz0iaHR0cDovL2RvY3Mub2FzaXMtb3Blbi5vcmcvd3Mtc3gvd3Mtc2VjdXJlY29udmVyc2F0aW9uLzIwMDUxMiI+PElkZW50aWZpZXI+dXJuOnV1aWQ6MDE2ZTU2NTktM2Q3MC00YmQ1LTkyMTQtNThkMzBiNGUyNDczPC9JZGVudGlmaWVyPjwvU2VjdXJpdHlDb250ZXh0VG9rZW4+; _UserCulture=ru-RU; UniqueContextHash=1jsR4w4CwJoqBi4R98JjUA==

            //{}

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

        private static UserState Logout(UserState userState)
        {
            userState.Result = false;
            Console.WriteLine("---------------------------------------------------------------------");
            Console.WriteLine("- Фаза 7. Logout");
            Console.WriteLine("---------------------------------------------------------------------");
            //POST https://my.kaspersky.com/SignIn/SignOutTo HTTP/1.1
            //Host: my.kaspersky.com
            //Connection: keep - alive
            //Content - Length: 189
            //Cache - Control: max - age = 0
            //Origin: https://my.kaspersky.com
            //            Upgrade - Insecure - Requests: 1
            //Content - Type: application / x - www - form - urlencoded
            //User - Agent: Mozilla / 5.0(Windows NT 6.1) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 67.0.3396.99 Safari / 537.36
            //Accept: text / html,application / xhtml + xml,application / xml; q = 0.9,image / webp,image / apng,*/*;q=0.8
            //Referer: https://my.kaspersky.com/dr/Store
            //Accept-Encoding: gzip, deflate, br
            //Accept-Language: ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7
            //Cookie: SessionID=3bwmzvgjicxusiyta2dbsjo2; __RequestVerificationToken=Ygokk26uqKvuVu6bQHL1w5CL0Opfwy6pauUunZv82o3VtNixxVPHAqN0Ry_OFSBXvLkK4UI1Z7noinmht5TG99ARH0eMyeAA120BpPNmBNA1; MyKFedAuth=77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz48U2VjdXJpdHlDb250ZXh0VG9rZW4gcDE6SWQ9Il8yZTViN2U1OS1hNzVlLTQzNTctYjhjYy05NWQ1Y2MyODlhNDQtMEVCOUMzRDYwOURFNzQzRDdEMjc5ODlFQzE0QTBFQkIiIHhtbG5zOnAxPSJodHRwOi8vZG9jcy5vYXNpcy1vcGVuLm9yZy93c3MvMjAwNC8wMS9vYXNpcy0yMDA0MDEtd3NzLXdzc2VjdXJpdHktdXRpbGl0eS0xLjAueHNkIiB4bWxucz0iaHR0cDovL2RvY3Mub2FzaXMtb3Blbi5vcmcvd3Mtc3gvd3Mtc2VjdXJlY29udmVyc2F0aW9uLzIwMDUxMiI+PElkZW50aWZpZXI+dXJuOnV1aWQ6ZThiZTU3ZjMtZmI4YS00MDA0LWJjZmItNTU1ZjYzZDUzYjIwPC9JZGVudGlmaWVyPjwvU2VjdXJpdHlDb250ZXh0VG9rZW4+; _UserCulture=ru-RU; UniqueContextHash=1jsR4w4CwJoqBi4R98JjUA==

            //__RequestVerificationToken=2PQJM4hITUS4ORZiA5uZ7qrmAamjlGw-Ie-rMh3gUgSlDPrjW5yTPpBVr7UJTYBcAGlnqs7cbxbH5yeZvtSU_Icne9Xcwtx-zgHZ6t_-SbbqVEmRm-VAz6slseIUnqM7hjDTVccjrlXm5aHk_AvxKg2&returnUrl=

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
