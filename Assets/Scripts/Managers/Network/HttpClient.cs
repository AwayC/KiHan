using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using KiHan.Config;

namespace Managers
{
    [Serializable]
    public class HttpResponse
    {
        public int code;
        public string msg;
    }

    [Serializable]
    public class TokenData
    {
        public string token;
    }

    [Serializable]
    public class LoginResponse : HttpResponse
    {
        public TokenData data;
    }

    [Serializable]
    public class RegisterRequest
    {
        public string username;
        public string password;
        public string email;
    }

    [Serializable]
    public class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class UpdateEmailRequest
    {
        public string email;
        public string token;
    }

    [Serializable]
    public class UpdatePasswordRequest
    {
        public string password;
        public string token;
    }

    [Serializable]
    public class VersionRequest
    {
        public string version;
    }

    [Serializable]
    public class VersionResponse : HttpResponse
    {
        public string version;
    }

    public class HttpClient : UnitySingleton<HttpClient>
    {
        public string Token { get; private set; }
        public bool IsVersionValid { get; private set; } = true;

        public static string MD5Hash(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private IEnumerator PostRequest<T>(string url, string jsonBody, Action<T> onSuccess, Action<string> onError) where T : HttpResponse
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                bool isNetworkError = request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError;

                if (isNetworkError)
                {
                    onError?.Invoke(request.error);
                }
                else
                {
                    try
                    {
                        string jsonRes = request.downloadHandler.text;
                        T res = JsonUtility.FromJson<T>(jsonRes);
                        
                        // 即使是 ProtocolError (如 HTTP 400)，只要服务端返回了规范的 JSON 错误体，就交由 onSuccess 让业务逻辑显示错误信息
                        if (res != null && (request.result == UnityWebRequest.Result.Success || res.code != 0 || !string.IsNullOrEmpty(res.msg)))
                        {
                            onSuccess?.Invoke(res);
                        }
                        else
                        {
                            onError?.Invoke(request.error);
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke("JSON 解析错误: " + e.Message);
                    }
                }
            }
        }

        public void Register(string username, string password, string email, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            string url = HttpConfig.LoginServerUrl + "/api/register";
            string hashPwd = MD5Hash(password);
            RegisterRequest req = new RegisterRequest { username = username, password = hashPwd, email = email };
            string json = JsonUtility.ToJson(req);
            
            StartCoroutine(PostRequest<LoginResponse>(url, json, res => {
                if (res.code == (int)HttpErrCode.Ok && res.data != null) Token = res.data.token;
                onSuccess?.Invoke(res);
            }, onError));
        }

        public void Login(string username, string password, Action<LoginResponse> onSuccess, Action<string> onError)
        {
            string url = HttpConfig.LoginServerUrl + "/api/login";
            string hashPwd = MD5Hash(password);
            LoginRequest req = new LoginRequest { username = username, password = hashPwd };
            string json = JsonUtility.ToJson(req);
            
            StartCoroutine(PostRequest<LoginResponse>(url, json, res => {
                if (res.code == (int)HttpErrCode.Ok && res.data != null) Token = res.data.token;
                onSuccess?.Invoke(res);
            }, onError));
        }

        public void UpdateEmail(string email, Action<HttpResponse> onSuccess, Action<string> onError)
        {
            string url = HttpConfig.LoginServerUrl + "/api/update_email";
            UpdateEmailRequest req = new UpdateEmailRequest { email = email, token = Token };
            string json = JsonUtility.ToJson(req);
            StartCoroutine(PostRequest<HttpResponse>(url, json, onSuccess, onError));
        }

        public void UpdatePassword(string password, Action<HttpResponse> onSuccess, Action<string> onError)
        {
            string url = HttpConfig.LoginServerUrl + "/api/update_password";
            string hashPwd = MD5Hash(password);
            UpdatePasswordRequest req = new UpdatePasswordRequest { password = hashPwd, token = Token };
            string json = JsonUtility.ToJson(req);
            StartCoroutine(PostRequest<HttpResponse>(url, json, onSuccess, onError));
        }

        public void CheckVersion(string version, Action<VersionResponse> onSuccess, Action<string> onError)
        {
            string url = HttpConfig.LoginServerUrl + "/api/check_version";
            VersionRequest req = new VersionRequest { version = version };
            string json = JsonUtility.ToJson(req);

            StartCoroutine(PostRequest<VersionResponse>(url, json, res =>
            {
                if (res.code == (int)HttpErrCode.InvalidVersion)
                {
                    IsVersionValid = false;
                }
                else if (res.code == (int)HttpErrCode.Ok)
                {
                    IsVersionValid = true;
                }
                onSuccess?.Invoke(res);
            }, onError));
        }

        public void ClearToken()
        {
            Token = null;
        }
    }
}