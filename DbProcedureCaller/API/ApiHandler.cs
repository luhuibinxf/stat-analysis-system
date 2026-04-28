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
                else if (url == "/get-system-types" && httpMethod == "GET")
                {
                    return HandleGetSystemTypes();
                }
                else if (url.StartsWith("/get-reporters"))
                {
                    return HandleGetReporters(url);
                }
                else if (url.StartsWith("/get-reviewers"))
                {
                    return HandleGetReviewers(url);
                }
                else if (url.StartsWith("/get-departments"))
                {
                    return HandleGetDepartments(url);
                }
                else if (url.StartsWith("/get-patient-types"))
                {
                    return HandleGetPatientTypes(url);
                }
                else if (url.StartsWith("/get-result-status"))
                {
                    return HandleGetResultStatus();
                }
                else if (url == "/get-categories" && httpMethod == "GET")
                {
                    return HandleGetCategories(HttpUtility.ParseQueryString(url.Contains("?") ? url.Split('?')[1] : ""));
                }
                else if (url == "/get-hospital-info" && httpMethod == "GET")
                {
                    return HandleGetHospitalInfo();
                }
                else if (url == "/update-db-config" && httpMethod == "POST")
                {
                    return HandleUpdateDbConfig(inputStream);
                }
                else
                {
                    return Encoding.UTF8.GetBytes("{\"success\": false, \"error\": \"未知的API端点\"}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "处理API请求失败");
                return Encoding.UTF8.GetBytes($"{{\"success\": false, \"error\": \"{ex.Message}\"}}");
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

        private byte[] HandleGetSystemTypes()
        {
            string json = _dailyAnalysisService.GetSystemTypes();
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleGetReporters(string url)
        {
            string system = "";
            if (url.Contains("?system="))
            {
                system = url.Substring(url.IndexOf("?system=") + 8);
                if (system.Contains("&"))
                {
                    system = system.Substring(0, system.IndexOf("&"));
                }
            }

            string json = _dailyAnalysisService.GetReporters(system);
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleGetReviewers(string url)
        {
            string system = "";
            if (url.Contains("?system="))
            {
                system = url.Substring(url.IndexOf("?system=") + 8);
                if (system.Contains("&"))
                {
                    system = system.Substring(0, system.IndexOf("&"));
                }
            }

            string json = _dailyAnalysisService.GetReviewers(system);
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleGetDepartments(string url)
        {
            string system = "";
            if (url.Contains("?system="))
            {
                system = url.Substring(url.IndexOf("?system=") + 8);
                if (system.Contains("&"))
                {
                    system = system.Substring(0, system.IndexOf("&"));
                }
            }

            string json = _dailyAnalysisService.GetDepartments(system);
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleGetPatientTypes(string url)
        {
            string system = "";
            if (url.Contains("?system="))
            {
                system = url.Substring(url.IndexOf("?system=") + 8);
                if (system.Contains("&"))
                {
                    system = system.Substring(0, system.IndexOf("&"));
                }
            }

            string json = _dailyAnalysisService.GetPatientTypes(system);
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleGetResultStatus()
        {
            string json = _dailyAnalysisService.GetResultStatusTypes();
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] HandleGetCategories(System.Collections.Specialized.NameValueCollection queryString)
        {
            string system = queryString["system"] ?? "";
            return Encoding.UTF8.GetBytes("{\"success\": true, \"data\": [{\"code\": \"CT\", \"name\": \"CT\"}, {\"code\": \"MRI\", \"name\": \"核磁共振\"}, {\"code\": \"超声\", \"name\": \"超声\"}]}");
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
            return Encoding.UTF8.GetBytes($"{{\"success\": true, \"hospitalName\": \"{hospitalName}\"}}");
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
                    return Encoding.UTF8.GetBytes($"{{\"success\": false, \"error\": \"{ex.Message}\"}}");
                }
            }
        }

        private string ConvertDataTableToJson(DataTable dt)
        {
            var rows = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
            foreach (DataRow dr in dt.Rows)
            {
                var row = new System.Collections.Generic.Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    row.Add(col.ColumnName, dr[col] != DBNull.Value ? dr[col] : null);
                }
                rows.Add(row);
            }
            return string.Format("{{\"success\": true, \"data\": {0}}}",
                Newtonsoft.Json.JsonConvert.SerializeObject(rows));
        }
    }
}