using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using DbProcedureCaller.Core;
using DbProcedureCaller.Config;

namespace DbProcedureCaller.Services
{
    public class DailyAnalysisService
    {
        public DataTable GetAnalysisData(
            string startDate = "",
            string endDate = "",
            string system = "",
            string reporter = "",
            string reviewer = "",
            string technician = "",
            string department = "",
            string category = "",
            string patientType = "",
            string resultStatus = "",
            string groupBy = "",
            string sortBy = "任务数量",
            string sortOrder = "DESC",
            int pageSize = 0,
            int pageIndex = 1)
        {
            DataTable dt = new DataTable();
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空");
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add("请先配置数据库连接");
                return dt;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    StringBuilder sqlBuilder = BuildQuerySql(startDate, endDate, system, reporter, reviewer, technician, department, category, patientType, resultStatus, groupBy, sortBy, sortOrder, pageSize, pageIndex);

                    using (SqlCommand cmd = new SqlCommand(sqlBuilder.ToString(), conn))
                    {
                        AddParameters(cmd, startDate, endDate, system, reporter, reviewer, technician, department, category, patientType, resultStatus);

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }

                    RecordQueryHistory(conn, startDate, endDate, system, reporter, reviewer, technician, department, category, dt.Rows.Count, null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取统计数据失败");
                RecordQueryHistory(null, startDate, endDate, system, reporter, reviewer, technician, department, category, 0, ex.Message);
                dt = new DataTable();
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        private StringBuilder BuildQuerySql(
            string startDate,
            string endDate,
            string system,
            string reporter,
            string reviewer,
            string technician,
            string department,
            string category,
            string patientType,
            string resultStatus,
            string groupBy,
            string sortBy,
            string sortOrder,
            int pageSize,
            int pageIndex)
        {
            StringBuilder sqlBuilder = new StringBuilder();
            sqlBuilder.Append(@"
                SELECT
                    ISNULL(t.SYSTEM_SOURCE_NO, '') AS 系统,
                    ISNULL(r.REPORTER_NAME, '') AS 报告医生,
                    ISNULL(r.REVIEWER_NAME, '') AS 审核医生,
                    ISNULL(t.TECHNICIAN_NAME, '') AS 技师,
                    ISNULL(t.EXEC_DEPT_NAME, '') AS 执行科室,
                    ISNULL(t.EXAM_CATEGORY_NAME, '') AS 检查类型,
                    ISNULL(t.ENCOUNTER_TYPE_NAME, '') AS 病人类型,
                    ISNULL(r.REPORT_RESULT_STATUS, '') AS 结果状态,
                    COUNT(*) AS 任务数量,
                    ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                    ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阴性' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                    ISNULL(AVG(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                FROM EXAM_TASK t
                LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                WHERE t.IS_DEL = 0");

            List<string> conditions = new List<string>();

            if (!string.IsNullOrEmpty(startDate))
                conditions.Add("t.CREATED_AT >= @StartDate");
            if (!string.IsNullOrEmpty(endDate))
                conditions.Add("t.CREATED_AT < DATEADD(DAY, 1, @EndDate)");
            if (!string.IsNullOrEmpty(system))
                conditions.Add(BuildInCondition("t.SYSTEM_SOURCE_NO", system));
            if (!string.IsNullOrEmpty(reporter))
                conditions.Add(BuildInCondition("r.REPORTER_NAME", reporter));
            if (!string.IsNullOrEmpty(reviewer))
                conditions.Add(BuildInCondition("r.REVIEWER_NAME", reviewer));
            if (!string.IsNullOrEmpty(technician))
                conditions.Add(BuildInCondition("t.TECHNICIAN_NAME", technician));
            if (!string.IsNullOrEmpty(department))
                conditions.Add(BuildInCondition("t.EXEC_DEPT_NAME", department));
            if (!string.IsNullOrEmpty(category))
                conditions.Add(BuildInCondition("t.EXAM_CATEGORY_NAME", category));
            if (!string.IsNullOrEmpty(patientType))
                conditions.Add(BuildInCondition("t.ENCOUNTER_TYPE_NAME", patientType));
            if (!string.IsNullOrEmpty(resultStatus))
                conditions.Add(BuildInCondition("r.REPORT_RESULT_STATUS", resultStatus));

            if (conditions.Count > 0)
                sqlBuilder.Append(" AND " + string.Join(" AND ", conditions));

            sqlBuilder.Append(@"
                GROUP BY
                    ISNULL(t.SYSTEM_SOURCE_NO, ''),
                    ISNULL(r.REPORTER_NAME, ''),
                    ISNULL(r.REVIEWER_NAME, ''),
                    ISNULL(t.TECHNICIAN_NAME, ''),
                    t.EXEC_DEPT_NAME,
                    ISNULL(t.EXAM_CATEGORY_NAME, ''),
                    ISNULL(t.ENCOUNTER_TYPE_NAME, ''),
                    ISNULL(r.REPORT_RESULT_STATUS, '')");

            string validSortBy = ValidateSortField(sortBy);
            string validSortOrder = sortOrder.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.Append(" ORDER BY " + validSortBy + " " + validSortOrder);

            if (pageSize > 0)
            {
                int skip = (pageIndex - 1) * pageSize;
                sqlBuilder.Append(" OFFSET " + skip + " ROWS FETCH NEXT " + pageSize + " ROWS ONLY");
            }

            return sqlBuilder;
        }

        private string BuildInCondition(string field, string values)
        {
            if (values.Contains(","))
            {
                string[] valueArray = values.Split(',');
                string quotedValues = string.Join(",", valueArray.Select(v => "'" + v.Trim() + "'"));
                return field + " IN (" + quotedValues + ")";
            }
            return field + " = '" + values + "'";
        }

        private string ValidateSortField(string sortBy)
        {
            string[] validFields = { "系统", "报告医生", "审核医生", "技师", "执行科室", "检查类型", "病人类型", "结果状态", "任务数量", "阳性数量", "阴性数量", "阳性率" };
            return validFields.Contains(sortBy) ? sortBy : "任务数量";
        }

        private void AddParameters(SqlCommand cmd,
            string startDate, string endDate, string system,
            string reporter, string reviewer, string technician,
            string department, string category, string patientType, string resultStatus)
        {
            if (!string.IsNullOrEmpty(startDate))
                cmd.Parameters.AddWithValue("@StartDate", startDate);
            if (!string.IsNullOrEmpty(endDate))
                cmd.Parameters.AddWithValue("@EndDate", endDate);
        }

        private void RecordQueryHistory(SqlConnection conn,
            string startDate, string endDate, string system,
            string reporter, string reviewer, string technician,
            string department, string category,
            int rowCount, string errorMessage)
        {
            if (conn == null) return;

            try
            {
                string sql = @"INSERT INTO DAILY_QUERY_HISTORY
                    (QUERY_DATE, QUERY_TYPE, QUERY_PARAMS, EXECUTION_STATUS, ROW_COUNT, ERROR_MESSAGE)
                    VALUES (@queryDate, @queryType, @queryParams, @executionStatus, @rowCount, @errorMessage)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@queryDate", startDate);
                    cmd.Parameters.AddWithValue("@queryType", "daily");
                    cmd.Parameters.AddWithValue("@queryParams",
                        string.Format("{{\"startDate\": \"{0}\", \"endDate\": \"{1}\", \"system\": \"{2}\", \"reporter\": \"{3}\", \"reviewer\": \"{4}\", \"technician\": \"{5}\", \"department\": \"{6}\", \"category\": \"{7}\"}}",
                            startDate, endDate, system, reporter, reviewer, technician, department, category));
                    cmd.Parameters.AddWithValue("@executionStatus", string.IsNullOrEmpty(errorMessage) ? "成功" : "失败");
                    cmd.Parameters.AddWithValue("@rowCount", rowCount);
                    cmd.Parameters.AddWithValue("@errorMessage", errorMessage ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "记录查询历史失败");
            }
        }

        public DataTable GetDepartmentStatistics(string startDate, string endDate, string system = "")
        {
            DataTable dt = new DataTable();
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空");
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add("请先配置数据库连接");
                return dt;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = @"
                        SELECT
                            t.EXEC_DEPT_NAME AS 执行科室,
                            ISNULL(r.REPORT_RESULT_STATUS, '') AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阴性' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                        GROUP BY
                            t.EXEC_DEPT_NAME,
                            ISNULL(r.REPORT_RESULT_STATUS, '')
                        ORDER BY 任务数量 DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@System", system ?? "");

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取科室统计数据失败");
                dt = new DataTable();
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        public DataTable GetDoctorStatistics(string startDate, string endDate, string system = "", string doctorType = "reporter")
        {
            DataTable dt = new DataTable();
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空");
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add("请先配置数据库连接");
                return dt;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = doctorType == "reviewer" ? @"
                        SELECT
                            ISNULL(r.REVIEWER_NAME, '') AS 医生姓名,
                            '审核医生' AS 医生类型,
                            ISNULL(r.REPORT_RESULT_STATUS, '') AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阴性' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                            AND r.REVIEWER_NAME IS NOT NULL
                        GROUP BY
                            r.REVIEWER_NAME,
                            ISNULL(r.REPORT_RESULT_STATUS, '')
                        ORDER BY 任务数量 DESC" : @"
                        SELECT
                            ISNULL(r.REPORTER_NAME, '') AS 医生姓名,
                            '报告医生' AS 医生类型,
                            ISNULL(r.REPORT_RESULT_STATUS, '') AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阴性' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                            AND r.REPORTER_NAME IS NOT NULL
                        GROUP BY
                            r.REPORTER_NAME,
                            ISNULL(r.REPORT_RESULT_STATUS, '')
                        ORDER BY 任务数量 DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@System", system ?? "");

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取医生统计数据失败");
                dt = new DataTable();
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        public DataTable GetCategoryStatistics(string startDate, string endDate, string system = "")
        {
            DataTable dt = new DataTable();
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空");
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add("请先配置数据库连接");
                return dt;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = @"
                        SELECT
                            ISNULL(t.EXAM_CATEGORY_NAME, '') AS 检查类型,
                            t.EXEC_DEPT_NAME AS 所属科室,
                            ISNULL(r.REPORT_RESULT_STATUS, '') AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT_STATUS = '阴性' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT_STATUS = '阳性' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                        GROUP BY
                            t.EXAM_CATEGORY_NAME,
                            t.EXEC_DEPT_NAME,
                            ISNULL(r.REPORT_RESULT_STATUS, '')
                        ORDER BY 任务数量 DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", startDate);
                        cmd.Parameters.AddWithValue("@EndDate", endDate);
                        cmd.Parameters.AddWithValue("@System", system ?? "");

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取检查类型统计数据失败");
                dt = new DataTable();
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        public string GetQueryConfig()
        {
            try
            {
                string connectionString = ConnectionStrings.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    LogHelper.LogError("数据库连接字符串为空");
                    return "{\"success\": false, \"error\": \"请先配置数据库连接\", \"data\": []}";
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = @"SELECT 
                        ID, FIELD_NAME, DISPLAY_NAME, IS_VISIBLE, IS_MULTIPLE, 
                        SORT_ORDER, PARENT_FIELD, DEFAULT_VALUE, PLACEHOLDER, IS_ACTIVE
                        FROM tjfx_QUERY_CONFIG
                        WHERE IS_ACTIVE = 1
                        ORDER BY SORT_ORDER";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var list = new List<Dictionary<string, object>>();
                        while (reader.Read())
                        {
                            list.Add(new Dictionary<string, object>
                            {
                                { "id", reader["ID"] },
                                { "fieldName", reader["FIELD_NAME"].ToString() },
                                { "displayName", reader["DISPLAY_NAME"].ToString() },
                                { "isVisible", reader["IS_VISIBLE"] != DBNull.Value && Convert.ToBoolean(reader["IS_VISIBLE"]) },
                                { "isMultiple", reader["IS_MULTIPLE"] != DBNull.Value && Convert.ToBoolean(reader["IS_MULTIPLE"]) },
                                { "sortOrder", reader["SORT_ORDER"] != DBNull.Value ? Convert.ToInt32(reader["SORT_ORDER"]) : 0 },
                                { "parentField", reader["PARENT_FIELD"]?.ToString() ?? "" },
                                { "defaultValue", reader["DEFAULT_VALUE"]?.ToString() ?? "" },
                                { "placeholder", reader["PLACEHOLDER"]?.ToString() ?? "" }
                            });
                        }

                        return string.Format("{{\"success\": true, \"data\": {0}}}",
                            Newtonsoft.Json.JsonConvert.SerializeObject(list));
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取查询配置失败");
                return "{\"success\": false, \"error\": \"" + ex.Message.Replace("\"", "'") + "\", \"data\": []}";
            }
        }

        public string ExecuteDynamicQuery(string fieldName, string parentValue = "")
        {
            try
            {
                string connectionString = ConnectionStrings.GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    LogHelper.LogError("数据库连接字符串为空");
                    return "{\"success\": false, \"error\": \"请先配置数据库连接\", \"data\": []}";
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("usp_SafeExecuteDynamicQuery", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@FieldName", fieldName);
                        cmd.Parameters.AddWithValue("@ParentValue", parentValue ?? "");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var list = new List<Dictionary<string, string>>();
                            while (reader.Read())
                            {
                                list.Add(new Dictionary<string, string>
                                {
                                    { "code", reader["code"]?.ToString() ?? "" },
                                    { "name", reader["name"]?.ToString() ?? "" }
                                });
                            }

                            return string.Format("{{\"success\": true, \"data\": {0}}}",
                                Newtonsoft.Json.JsonConvert.SerializeObject(list));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "执行动态查询失败: " + fieldName);
                return "{\"success\": false, \"error\": \"" + ex.Message.Replace("\"", "'") + "\", \"data\": []}";
            }
        }

        public string GetHospitalName()
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogInfo("数据库连接字符串为空，使用默认医院名称");
                return "XX医院";
            }

            LogHelper.LogInfo("尝试从数据库获取医院名称...");

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    LogHelper.LogInfo("数据库连接成功");

                    string sql = @"SELECT TOP 1 CONFIG_VALUE FROM tjfx_SYS_CONFIG WHERE CONFIG_KEY = 'HOSPITAL_NAME' AND IS_DEL = 0";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            string hospitalName = result.ToString();
                            LogHelper.LogInfo("从tjfx_SYS_CONFIG获取医院名称: " + hospitalName);
                            return hospitalName;
                        }
                        LogHelper.LogInfo("tjfx_SYS_CONFIG表中未找到医院名称配置");
                    }

                    sql = @"SELECT TOP 1 HOSPITAL_NAME FROM EXAM_TASK WHERE HOSPITAL_NAME IS NOT NULL";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            string hospitalName = result.ToString();
                            LogHelper.LogInfo("从EXAM_TASK获取医院名称: " + hospitalName);
                            return hospitalName;
                        }
                        LogHelper.LogInfo("EXAM_TASK表中未找到医院名称配置");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取医院名称失败");
                LogHelper.LogError("数据库连接错误: " + ex.Message);
            }

            LogHelper.LogInfo("未找到医院名称配置，使用默认值");
            return "XX医院";
        }
    }
}
