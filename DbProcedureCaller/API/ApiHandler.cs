using System;
using System.Data;
using System.IO;
using System.Text;
using System.Web;
using DbProcedureCaller.Core;
using DbProcedureCaller.Services;

namespace DbProcedureCaller.API
{
    public class ApiHandler
    {
        private UserService _userService;
        private DailyAnalysisService _dailyAnalysisService;

        public ApiHandler()
        {
            _userService = new UserService();
            _dailyAnalysisService = new DailyAnalysisService();
        }

        public byte[] HandleRequest(string url, string httpMethod, Stream inputStream)
        {
            LogHelper.LogInfo($"API请求: {httpMethod} {url}");

            try
            {
                if (url.StartsWith("/login") && httpMethod == "POST")
                {
                    return HandleLogin(inputStream);
                }
                else if (url == "/get-users" && httpMethod == "GET")
                {
                    return HandleGetUsers();
                }
                else if (url == "/add-user" && httpMethod == "POST")
                {
                    return HandleAddUser(inputStream);
                }
                else if (url == "/update-user" && httpMethod == "POST")
                {
                    return HandleUpdateUser(inputStream);
                }
                else if (url == "/delete-user" && httpMethod == "POST")
                {
                    return HandleDeleteUser(inputStream);
                }
                else if (url == "/daily-analysis" && httpMethod == "POST")
                {
                    return HandleDailyAnalysis(inputStream);
                }
                else if (url == "/department-statistics" && httpMethod == "POST")
                {
                    return HandleDepartmentStatistics(inputStream);
                }
                else if (url == "/doctor-statistics" && httpMethod == "POST")
                {
                    return HandleDoctorStatistics(inputStream);
                }
                else if (url == "/category-statistics" && httpMethod == "POST")
                {
                    return HandleCategoryStatistics(inputStream);
                }
                else if (url == "/get-query-config" && httpMethod == "GET")
                {
                    return HandleGetQueryConfig();
                }
                else if (url.StartsWith("/execute-dynamic-query"))
                {
                    return HandleExecuteDynamicQuery(url);
                }
                else if (url == "/get-hospital-info" && httpMethod == "GET")
                {
                    return HandleGetHospitalInfo();
                }
                else if (url == "/update-db-config" && httpMethod == "POST")
                {
                    return HandleUpdateDbConfig(inputStream);
                }
                else if (url == "/get-port" && httpMethod == "GET")
                {
                    return HandleGetPort();
                }
                else if (url == "/set-port" && httpMethod == "POST")
                {
                    return HandleSetPort(inputStream);
                }
                else if (url == "/init-db" && httpMethod == "POST")
                {
                    return HandleInitDb();
                }
                else if (url.StartsWith("/get-all-options") && httpMethod == "GET")
                {
                    return HandleGetAllOptions(url);
                }
                else if (url == "/get-system-types" && httpMethod == "GET")
                {
                    return HandleGetSystemTypes();
                }
                else if (url.StartsWith("/get-reporters") && httpMethod == "GET")
                {
                    return HandleGetReporters(url);
                }
                else if (url.StartsWith("/get-reviewers") && httpMethod == "GET")
                {
                    return HandleGetReviewers(url);
                }
                else if (url.StartsWith("/get-categories") && httpMethod == "GET")
                {
                    return HandleGetCategories(url);
                }
                else if (url.StartsWith("/get-departments") && httpMethod == "GET")
                {
                    return HandleGetDepartments(url);
                }
                else if (url.StartsWith("/get-patient-types") && httpMethod == "GET")
                {
                    return HandleGetPatientTypes(url);
                }
                else if (url.StartsWith("/get-result-status") && httpMethod == "GET")
                {
                    return HandleGetResultStatus();
                }
                else
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"未知的API端点\"}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "处理API请求失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleLogin(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string username = ExtractValue(postData, "username");
                string password = ExtractValue(postData, "password");

                LogHelper.LogInfo($"登录尝试: 用户名={username}");

                if (_userService.ValidateUser(username, password))
                {
                    bool isAdmin = _userService.IsAdminUser(username);
                    LogHelper.LogInfo($"登录成功: 用户名={username}, 是管理员={isAdmin}");
                    return Encoding.UTF8.GetBytes("{\"success\": true, \"isAdmin\": " + isAdmin.ToString().ToLower() + "}");
                }
                else
                {
                    LogHelper.LogInfo($"登录失败: 用户名={username}");
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"用户名或密码错误\"}");
                }
            }
        }

        private byte[] HandleGetUsers()
        {
            string json = _userService.GetUsersJson();
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleAddUser(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                int id = int.Parse(ExtractValue(postData, "id"));
                string username = ExtractValue(postData, "username");
                string password = ExtractValue(postData, "password");
                string role = ExtractValue(postData, "role");
                string status = ExtractValue(postData, "status");

                bool success = _userService.AddUser(id, username, password, role, status);
                if (success)
                {
                    return Encoding.UTF8.GetBytes("{\"success\": true, \"message\": \"用户添加成功\"}");
                }
                else
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"用户ID或用户名已存在\"}");
                }
            }
        }

        private byte[] HandleUpdateUser(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                int id = int.Parse(ExtractValue(postData, "id"));
                string username = ExtractValue(postData, "username");
                string password = ExtractValue(postData, "password");
                string role = ExtractValue(postData, "role");
                string status = ExtractValue(postData, "status");

                bool success = _userService.UpdateUser(id, username, password, role, status);
                if (success)
                {
                    return Encoding.UTF8.GetBytes("{\"success\": true, \"message\": \"用户更新成功\"}");
                }
                else
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"用户更新失败或管理员用户不可修改\"}");
                }
            }
        }

        private byte[] HandleDeleteUser(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                int id = int.Parse(ExtractValue(postData, "id"));

                bool success = _userService.DeleteUser(id);
                if (success)
                {
                    return Encoding.UTF8.GetBytes("{\"success\": true, \"message\": \"用户删除成功\"}");
                }
                else
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"用户删除失败，管理员用户不可删除\"}");
                }
            }
        }

        private byte[] HandleDailyAnalysis(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string startDate = ExtractValue(postData, "startDate");
                string endDate = ExtractValue(postData, "endDate");
                string system = ExtractValue(postData, "system");
                string reporter = ExtractValue(postData, "reporter");
                string reviewer = ExtractValue(postData, "reviewer");
                string technician = ExtractValue(postData, "technician");
                string department = ExtractValue(postData, "department");
                string category = ExtractValue(postData, "category");
                string patientType = ExtractValue(postData, "patientType");
                string resultStatus = ExtractValue(postData, "resultStatus");
                string sortBy = ExtractValue(postData, "sortBy");
                string sortOrder = ExtractValue(postData, "sortOrder");
                int pageSize = int.TryParse(ExtractValue(postData, "pageSize"), out int ps) ? ps : 0;
                int pageIndex = int.TryParse(ExtractValue(postData, "pageIndex"), out int pi) ? pi : 1;

                LogHelper.LogInfo($"每日分析请求: startDate={startDate}, endDate={endDate}, system={system}, reporter={reporter}, reviewer={reviewer}, technician={technician}, department={department}, category={category}, patientType={patientType}, resultStatus={resultStatus}, sortBy={sortBy}, sortOrder={sortOrder}, pageSize={pageSize}, pageIndex={pageIndex}");

                DataTable result = _dailyAnalysisService.GetAnalysisData(
                    startDate, endDate, system, reporter, reviewer, technician, department, category, patientType, resultStatus,
                    "", sortBy, sortOrder, pageSize, pageIndex);

                string json = ConvertDataTableToJson(result);
                return Encoding.UTF8.GetBytes(json);
            }
        }

        private byte[] HandleDepartmentStatistics(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string startDate = ExtractValue(postData, "startDate");
                string endDate = ExtractValue(postData, "endDate");
                string system = ExtractValue(postData, "system");

                LogHelper.LogInfo($"科室统计请求: startDate={startDate}, endDate={endDate}, system={system}");

                DataTable result = _dailyAnalysisService.GetDepartmentStatistics(startDate, endDate, system);

                string json = ConvertDataTableToJson(result);
                return Encoding.UTF8.GetBytes(json);
            }
        }

        private byte[] HandleDoctorStatistics(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string startDate = ExtractValue(postData, "startDate");
                string endDate = ExtractValue(postData, "endDate");
                string system = ExtractValue(postData, "system");
                string doctorType = ExtractValue(postData, "doctorType");

                LogHelper.LogInfo($"医生统计请求: startDate={startDate}, endDate={endDate}, system={system}, doctorType={doctorType}");

                DataTable result = _dailyAnalysisService.GetDoctorStatistics(startDate, endDate, system, doctorType);

                string json = ConvertDataTableToJson(result);
                return Encoding.UTF8.GetBytes(json);
            }
        }

        private byte[] HandleCategoryStatistics(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string startDate = ExtractValue(postData, "startDate");
                string endDate = ExtractValue(postData, "endDate");
                string system = ExtractValue(postData, "system");

                LogHelper.LogInfo($"检查类型统计请求: startDate={startDate}, endDate={endDate}, system={system}");

                DataTable result = _dailyAnalysisService.GetCategoryStatistics(startDate, endDate, system);

                string json = ConvertDataTableToJson(result);
                return Encoding.UTF8.GetBytes(json);
            }
        }

        private byte[] HandleGetQueryConfig()
        {
            string json = _dailyAnalysisService.GetQueryConfig();
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleExecuteDynamicQuery(string url)
        {
            string fieldName = "";
            string parentValue = "";
            
            if (url.Contains("?"))
            {
                string queryString = url.Substring(url.IndexOf("?") + 1);
                string[] parameters = queryString.Split('&');
                foreach (string param in parameters)
                {
                    string[] keyValue = param.Split('=');
                    if (keyValue.Length == 2)
                    {
                        if (keyValue[0] == "fieldName")
                            fieldName = System.Net.WebUtility.UrlDecode(keyValue[1]);
                        else if (keyValue[0] == "parentValue")
                            parentValue = System.Net.WebUtility.UrlDecode(keyValue[1]);
                    }
                }
            }

            LogHelper.LogInfo($"动态查询请求: fieldName={fieldName}, parentValue={parentValue}");
            string json = _dailyAnalysisService.ExecuteDynamicQuery(fieldName, parentValue);
            return Encoding.UTF8.GetBytes(json);
        }

        private string ExtractValue(string data, string key)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(key))
                return "";

            if (data.TrimStart().StartsWith("{"))
            {
                string pattern = string.Format("\"{0}\":", key);
                int startSearch = data.IndexOf(pattern);
                if (startSearch >= 0)
                {
                    startSearch += pattern.Length;
                    startSearch = data.IndexOfAny(new char[] { '"', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '{', '[', 't', 'f', 'n' }, startSearch);
                    if (startSearch >= 0)
                    {
                        if (data[startSearch] == '"')
                        {
                            startSearch++;
                            int endIndex = data.IndexOf('"', startSearch);
                            if (endIndex > startSearch)
                            {
                                return data.Substring(startSearch, endIndex - startSearch);
                            }
                        }
                        else
                        {
                            int endIndex = data.IndexOfAny(new char[] { ',', '}', ']', ' ', '\n', '\r' }, startSearch);
                            if (endIndex > startSearch)
                            {
                                return data.Substring(startSearch, endIndex - startSearch).Trim();
                            }
                            else if (endIndex == -1)
                            {
                                return data.Substring(startSearch).Trim();
                            }
                        }
                    }
                }
            }
            else
            {
                string formPattern = key + "=";
                int formStart = data.IndexOf(formPattern);
                if (formStart >= 0)
                {
                    formStart += formPattern.Length;
                    int formEnd = data.IndexOf('&', formStart);
                    if (formEnd > formStart)
                    {
                        return System.Net.WebUtility.UrlDecode(data.Substring(formStart, formEnd - formStart));
                    }
                    else if (formEnd == -1)
                    {
                        return System.Net.WebUtility.UrlDecode(data.Substring(formStart));
                    }
                }
            }
            return "";
        }

        private byte[] HandleGetHospitalInfo()
        {
            string hospitalName = _dailyAnalysisService.GetHospitalName();
            string encodedName = HttpUtility.HtmlEncode(hospitalName);
            return Encoding.UTF8.GetBytes($"{{\"success\": true, \"hospitalName\": \"{encodedName}\"}}");
        }

        private byte[] HandleUpdateDbConfig(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string server = ExtractValue(postData, "server");
                string database = ExtractValue(postData, "database");
                string username = ExtractValue(postData, "username");
                string password = ExtractValue(postData, "password");

                LogHelper.LogInfo("尝试更新数据库配置");

                try
                {
                    string connectionString = $"Server={server};Database={database};User ID={username};Password={password};Integrated Security=False;TrustServerCertificate=True;";
                    
                    DbProcedureCaller.Config.ConnectionStrings.ClearCache();
                    
                    System.IO.File.WriteAllText(@"d:\AI\tran\config.dat", connectionString);
                    
                    LogHelper.LogInfo("数据库配置更新成功");
                    return Encoding.UTF8.GetBytes("{\"success\": true, \"message\": \"数据库配置更新成功，请重启程序生效\"}");
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"更新数据库配置失败: {ex.Message}");
                    return CreateErrorResponse(ex.Message);
                }
            }
        }

        private byte[] HandleGetPort()
        {
            try
            {
                string exeDir = System.Windows.Forms.Application.StartupPath;
                string configFile = System.IO.Path.Combine(exeDir, "server_config.dat");
                string configPort = "9094";
                string runningPort = "9094";

                if (System.IO.File.Exists(configFile))
                {
                    string encrypted = System.IO.File.ReadAllText(configFile).Trim();
                    try
                    {
                        byte[] data = Convert.FromBase64String(encrypted);
                        configPort = Encoding.UTF8.GetString(data);
                    }
                    catch { }
                }

                return Encoding.UTF8.GetBytes($"{{\"success\": true, \"data\": {{\"port\": \"{configPort}\", \"runningPort\": \"{runningPort}\"}}}}");
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取端口配置失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleSetPort(Stream inputStream)
        {
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string postData = reader.ReadToEnd();
                string port = ExtractValue(postData, "port");

                if (string.IsNullOrEmpty(port))
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"端口号不能为空\"}");
                }

                int portNum;
                if (!int.TryParse(port, out portNum) || portNum < 1 || portNum > 65535)
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"端口号必须在1-65535之间\"}");
                }

                try
                {
                    string exeDir = System.Windows.Forms.Application.StartupPath;
                    string configFile = System.IO.Path.Combine(exeDir, "server_config.dat");
                    byte[] data = Encoding.UTF8.GetBytes(port);
                    string encrypted = Convert.ToBase64String(data);
                    System.IO.File.WriteAllText(configFile, encrypted);
                    
                    LogHelper.LogInfo($"端口配置已保存: {port}");
                    return Encoding.UTF8.GetBytes("{\"success\": true, \"message\": \"端口配置已保存，重启程序后生效\"}");
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "保存端口配置失败");
                    return CreateErrorResponse(ex.Message);
                }
            }
        }

        private byte[] HandleInitDb()
        {
            LogHelper.LogInfo("开始初始化数据库配置表");
            try
            {
                string result = _dailyAnalysisService.InitializeConfigTables();
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "初始化数据库配置表失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetAllOptions(string url)
        {
            LogHelper.LogInfo("获取所有下拉框选项（聚合API）");
            try
            {
                string system = ExtractUrlParam(url, "system");
                string result = _dailyAnalysisService.GetAllOptions(system);
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取所有下拉框选项失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetSystemTypes()
        {
            LogHelper.LogInfo("获取系统类型列表");
            try
            {
                string result = _dailyAnalysisService.GetSystemTypes();
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取系统类型失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetReporters(string url)
        {
            LogHelper.LogInfo("获取报告医生列表");
            try
            {
                string system = ExtractUrlParam(url, "system");
                string result = _dailyAnalysisService.GetReporters(system);
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取报告医生失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetReviewers(string url)
        {
            LogHelper.LogInfo("获取审核医生列表");
            try
            {
                string system = ExtractUrlParam(url, "system");
                string result = _dailyAnalysisService.GetReviewers(system);
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取审核医生失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetCategories(string url)
        {
            LogHelper.LogInfo("获取检查类型列表");
            try
            {
                string system = ExtractUrlParam(url, "system");
                string result = _dailyAnalysisService.GetCategories(system);
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取检查类型失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetDepartments(string url)
        {
            LogHelper.LogInfo("获取执行科室列表");
            try
            {
                string system = ExtractUrlParam(url, "system");
                string result = _dailyAnalysisService.GetDepartments(system);
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取执行科室失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetPatientTypes(string url)
        {
            LogHelper.LogInfo("获取病人类型列表");
            try
            {
                string system = ExtractUrlParam(url, "system");
                string result = _dailyAnalysisService.GetPatientTypes(system);
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取病人类型失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private byte[] HandleGetResultStatus()
        {
            LogHelper.LogInfo("获取结果状态列表");
            try
            {
                string result = _dailyAnalysisService.GetResultStatus();
                return Encoding.UTF8.GetBytes(result);
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取结果状态失败");
                return CreateErrorResponse(ex.Message);
            }
        }

        private string ExtractUrlParam(string url, string paramName)
        {
            if (url.Contains("?" + paramName + "="))
            {
                string param = url.Substring(url.IndexOf("?" + paramName + "=") + paramName.Length + 2);
                if (param.Contains("&"))
                {
                    param = param.Substring(0, param.IndexOf("&"));
                }
                return param;
            }
            return "";
        }

        /// <summary>
        /// 将DataTable转换为JSON字符串（带XSS防护）
        /// 2026-04-29 修改：添加HTML编码防止XSS攻击
        /// </summary>
        /// <param name="dt">DataTable数据</param>
        /// <returns>JSON字符串</returns>
        private string ConvertDataTableToJson(DataTable dt)
        {
            var rows = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
            foreach (DataRow dr in dt.Rows)
            {
                var row = new System.Collections.Generic.Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    object value = dr[col] != DBNull.Value ? dr[col] : null;
                    if (value is string)
                    {
                        // XSS防护：对字符串值进行HTML编码
                        row.Add(col.ColumnName, HttpUtility.HtmlEncode(value.ToString()));
                    }
                    else
                    {
                        row.Add(col.ColumnName, value);
                    }
                }
                rows.Add(row);
            }
            return string.Format("{{\"success\": true, \"data\": {0}}}",
                Newtonsoft.Json.JsonConvert.SerializeObject(rows));
        }

        /// <summary>
        /// 创建统一的错误响应（带XSS防护）
        /// 2026-04-29 新增：统一错误处理机制，确保错误消息经过HTML编码
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>错误响应字节数组</returns>
        private byte[] CreateErrorResponse(string errorMessage)
        {
            string encodedMessage = HttpUtility.HtmlEncode(errorMessage);
            return Encoding.UTF8.GetBytes($"{{\"success\": false, \"error\": \"{encodedMessage}\"}}");
        }
    }
}