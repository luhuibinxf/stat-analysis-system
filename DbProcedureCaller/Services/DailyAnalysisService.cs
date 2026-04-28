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

            (string actualStartDate, string actualEndDate) = ResolveDateRange(startDate, endDate);

            if (string.IsNullOrEmpty(connectionString))
            {
                return GetMockData(actualStartDate, actualEndDate, system, reporter, reviewer, technician, department, category, patientType, resultStatus, sortBy, sortOrder, pageSize, pageIndex);
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    StringBuilder sqlBuilder = BuildQuerySql(actualStartDate, actualEndDate, system, reporter, reviewer, technician, department, category, patientType, resultStatus, groupBy, sortBy, sortOrder, pageSize, pageIndex);

                    using (SqlCommand cmd = new SqlCommand(sqlBuilder.ToString(), conn))
                    {
                        AddParameters(cmd, actualStartDate, actualEndDate, system, reporter, reviewer, technician, department, category, patientType, resultStatus);

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }

                    RecordQueryHistory(conn, actualStartDate, actualEndDate, system, reporter, reviewer, technician, department, category, dt.Rows.Count, null);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取统计数据失败");
                RecordQueryHistory(null, actualStartDate, actualEndDate, system, reporter, reviewer, technician, department, category, 0, ex.Message);
                dt = new DataTable();
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        private (string, string) ResolveDateRange(string startDate, string endDate)
        {
            DateTime now = DateTime.Now;

            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                return (startDate, endDate);
            }

            switch (startDate.ToLower())
            {
                case "today":
                    return (now.ToString("yyyy-MM-dd"), now.ToString("yyyy-MM-dd"));
                case "yesterday":
                    DateTime yesterday = now.AddDays(-1);
                    return (yesterday.ToString("yyyy-MM-dd"), yesterday.ToString("yyyy-MM-dd"));
                case "thisweek":
                    DayOfWeek day = now.DayOfWeek;
                    int diff = day - DayOfWeek.Monday;
                    DateTime monday = now.AddDays(-diff);
                    return (monday.ToString("yyyy-MM-dd"), now.ToString("yyyy-MM-dd"));
                case "lastweek":
                    DateTime lastWeekStart = now.AddDays(-7 - (int)now.DayOfWeek + 1);
                    DateTime lastWeekEnd = lastWeekStart.AddDays(6);
                    return (lastWeekStart.ToString("yyyy-MM-dd"), lastWeekEnd.ToString("yyyy-MM-dd"));
                case "thismonth":
                    DateTime monthStart = new DateTime(now.Year, now.Month, 1);
                    return (monthStart.ToString("yyyy-MM-dd"), now.ToString("yyyy-MM-dd"));
                case "lastmonth":
                    DateTime lastMonthStart = now.AddMonths(-1);
                    lastMonthStart = new DateTime(lastMonthStart.Year, lastMonthStart.Month, 1);
                    DateTime lastMonthEnd = new DateTime(now.Year, now.Month, 1).AddDays(-1);
                    return (lastMonthStart.ToString("yyyy-MM-dd"), lastMonthEnd.ToString("yyyy-MM-dd"));
                default:
                    return (startDate, endDate);
            }
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
                    dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME) AS 执行科室,
                    ISNULL(t.EXAM_CATEGORY_NAME, '') AS 检查类型,
                    ISNULL(ti.PATIENT_TYPE, '') AS 病人类型,
                    CASE
                        WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                        WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                        ELSE '未知'
                    END AS 结果状态,
                    COUNT(*) AS 任务数量,
                    ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                    ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阴性%' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                    ISNULL(AVG(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                FROM EXAM_TASK t
                LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                LEFT JOIN EXAM_TASK_INFO ti ON t.EXAM_TASK_ID = ti.EXAM_TASK_ID
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
                conditions.Add(BuildInCondition("dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME)", department));
            if (!string.IsNullOrEmpty(category))
                conditions.Add(BuildInCondition("t.EXAM_CATEGORY_NAME", category));
            if (!string.IsNullOrEmpty(patientType))
                conditions.Add(BuildInCondition("ti.PATIENT_TYPE", patientType));
            if (!string.IsNullOrEmpty(resultStatus))
            {
                if (resultStatus.Contains(","))
                {
                    string[] statuses = resultStatus.Split(',');
                    string statusList = string.Join(",", statuses.Select(s => $"'{s.Trim()}'"));
                    conditions.Add($"CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性' WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性' ELSE '未知' END IN ({statusList})");
                }
                else if (resultStatus == "阳性")
                    conditions.Add("r.REPORT_RESULT LIKE '%阳性%'");
                else if (resultStatus == "阴性")
                    conditions.Add("r.REPORT_RESULT LIKE '%阴性%'");
            }

            if (conditions.Count > 0)
                sqlBuilder.Append(" AND " + string.Join(" AND ", conditions));

            sqlBuilder.Append(@"
                GROUP BY
                    ISNULL(t.SYSTEM_SOURCE_NO, ''),
                    ISNULL(r.REPORTER_NAME, ''),
                    ISNULL(r.REVIEWER_NAME, ''),
                    ISNULL(t.TECHNICIAN_NAME, ''),
                    dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME),
                    ISNULL(t.EXAM_CATEGORY_NAME, ''),
                    ISNULL(ti.PATIENT_TYPE, ''),
                    CASE
                        WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                        WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                        ELSE '未知'
                    END");

            string validSortBy = ValidateSortField(sortBy);
            string validSortOrder = sortOrder.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.Append($" ORDER BY {validSortBy} {validSortOrder}");

            if (pageSize > 0)
            {
                int skip = (pageIndex - 1) * pageSize;
                sqlBuilder.Append($" OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY");
            }

            return sqlBuilder;
        }

        private string BuildInCondition(string field, string values)
        {
            if (values.Contains(","))
            {
                string[] valueArray = values.Split(',');
                string quotedValues = string.Join(",", valueArray.Select(v => $"'{v.Trim()}'"));
                return $"{field} IN ({quotedValues})";
            }
            return $"{field} = '{values}'";
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

        private DataTable GetMockData(
            string startDate, string endDate, string system,
            string reporter, string reviewer, string technician,
            string department, string category, string patientType, string resultStatus,
            string sortBy = "任务数量", string sortOrder = "DESC",
            int pageSize = 0, int pageIndex = 1)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("系统", typeof(string));
            dt.Columns.Add("报告医生", typeof(string));
            dt.Columns.Add("审核医生", typeof(string));
            dt.Columns.Add("技师", typeof(string));
            dt.Columns.Add("执行科室", typeof(string));
            dt.Columns.Add("检查类型", typeof(string));
            dt.Columns.Add("病人类型", typeof(string));
            dt.Columns.Add("结果状态", typeof(string));
            dt.Columns.Add("任务数量", typeof(int));
            dt.Columns.Add("阳性数量", typeof(int));
            dt.Columns.Add("阴性数量", typeof(int));
            dt.Columns.Add("阳性率", typeof(decimal));

            var rows = new List<object[]>
            {
                new object[] { system, reporter, reviewer, technician, department, category, patientType, "阳性", 100, 25, 75, 25.0m },
                new object[] { system, reporter, reviewer, technician, department, category, patientType, "阴性", 80, 5, 75, 6.25m },
                new object[] { "RIS", "张医生", "赵医生", "李技师", "放射科", "CT", "门诊", "阳性", 60, 18, 42, 30.0m },
                new object[] { "UIS", "李医生", "钱医生", "王技师", "超声科", "超声", "住院", "阳性", 50, 12, 38, 24.0m },
                new object[] { "EIS", "王医生", "孙医生", "张技师", "消化内镜(总)", "胃镜(新)", "门诊", "阳性", 40, 10, 30, 25.0m },
                new object[] { "RIS", "张医生", "赵医生", "李技师", "放射科", "核磁共振", "住院", "阴性", 35, 2, 33, 5.71m },
                new object[] { "UIS", "李医生", "钱医生", "王技师", "超声科", "介入超声", "急诊", "阳性", 30, 8, 22, 26.67m },
                new object[] { "RIS", "赵医生", "周医生", "刘技师", "放射科", "普放", "体检", "阴性", 25, 1, 24, 4.0m }
            };

            IEnumerable<object[]> filteredRows = rows;

            if (!string.IsNullOrEmpty(resultStatus))
            {
                if (resultStatus.Contains(","))
                {
                    var statusList = resultStatus.Split(',').Select(s => s.Trim()).ToList();
                    filteredRows = filteredRows.Where(r => statusList.Contains((string)r[7]));
                }
                else
                {
                    filteredRows = filteredRows.Where(r => (string)r[7] == resultStatus);
                }
            }

            int sortIndex = 8;
            switch (sortBy)
            {
                case "系统": sortIndex = 0; break;
                case "报告医生": sortIndex = 1; break;
                case "审核医生": sortIndex = 2; break;
                case "技师": sortIndex = 3; break;
                case "执行科室": sortIndex = 4; break;
                case "检查类型": sortIndex = 5; break;
                case "病人类型": sortIndex = 6; break;
                case "结果状态": sortIndex = 7; break;
                case "阳性数量": sortIndex = 9; break;
                case "阴性数量": sortIndex = 10; break;
                case "阳性率": sortIndex = 11; break;
            }

            filteredRows = sortOrder == "ASC"
                ? filteredRows.OrderBy(r => r[sortIndex])
                : filteredRows.OrderByDescending(r => r[sortIndex]);

            if (pageSize > 0)
            {
                filteredRows = filteredRows.Skip((pageIndex - 1) * pageSize).Take(pageSize);
            }

            foreach (var row in filteredRows)
            {
                dt.Rows.Add(row);
            }

            return dt;
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
                        string.Format("{{'startDate': '{0}', 'endDate': '{1}', 'system': '{2}', 'reporter': '{3}', 'reviewer': '{4}', 'technician': '{5}', 'department': '{6}', 'category': '{7}'}}",
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

            (string actualStartDate, string actualEndDate) = ResolveDateRange(startDate, endDate);

            if (string.IsNullOrEmpty(connectionString))
            {
                return GetMockDepartmentData();
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = @"
                        SELECT
                            dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME) AS 执行科室,
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阴性%' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                        GROUP BY
                            dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME),
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END
                        ORDER BY 任务数量 DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", actualStartDate);
                        cmd.Parameters.AddWithValue("@EndDate", actualEndDate);
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
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        public DataTable GetDoctorStatistics(string startDate, string endDate, string system = "", string doctorType = "reporter")
        {
            DataTable dt = new DataTable();
            string connectionString = ConnectionStrings.GetConnectionString();

            (string actualStartDate, string actualEndDate) = ResolveDateRange(startDate, endDate);

            if (string.IsNullOrEmpty(connectionString))
            {
                return GetMockDoctorData(doctorType);
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = doctorType == "reviewer" ? @"
                        SELECT
                            ISNULL(r.REVIEWER_NAME, '') AS 医生姓名,
                            '审核医生' AS 医生类型,
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阴性%' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                            AND r.REVIEWER_NAME IS NOT NULL
                        GROUP BY
                            r.REVIEWER_NAME,
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END
                        ORDER BY 任务数量 DESC" : @"
                        SELECT
                            ISNULL(r.REPORTER_NAME, '') AS 医生姓名,
                            '报告医生' AS 医生类型,
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阴性%' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                            AND r.REPORTER_NAME IS NOT NULL
                        GROUP BY
                            r.REPORTER_NAME,
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END
                        ORDER BY 任务数量 DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", actualStartDate);
                        cmd.Parameters.AddWithValue("@EndDate", actualEndDate);
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
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        public DataTable GetCategoryStatistics(string startDate, string endDate, string system = "")
        {
            DataTable dt = new DataTable();
            string connectionString = ConnectionStrings.GetConnectionString();

            (string actualStartDate, string actualEndDate) = ResolveDateRange(startDate, endDate);

            if (string.IsNullOrEmpty(connectionString))
            {
                return GetMockCategoryData();
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    string sql = @"
                        SELECT
                            ISNULL(t.EXAM_CATEGORY_NAME, '') AS 检查类型,
                            dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME) AS 所属科室,
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END AS 结果状态,
                            COUNT(*) AS 任务数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 1 ELSE 0 END), 0) AS 阳性数量,
                            ISNULL(SUM(CASE WHEN r.REPORT_RESULT LIKE '%阴性%' THEN 1 ELSE 0 END), 0) AS 阴性数量,
                            ISNULL(AVG(CASE WHEN r.REPORT_RESULT LIKE '%阳性%' THEN 100.0 ELSE 0 END), 0) AS 阳性率
                        FROM EXAM_TASK t
                        LEFT JOIN EXAM_REPORT r ON t.EXAM_TASK_ID = r.EXAM_TASK_ID
                        WHERE t.IS_DEL = 0
                            AND t.CREATED_AT >= @StartDate
                            AND t.CREATED_AT < DATEADD(DAY, 1, @EndDate)
                            AND (@System IS NULL OR @System = '' OR t.SYSTEM_SOURCE_NO = @System)
                        GROUP BY
                            t.EXAM_CATEGORY_NAME,
                            dbo.fn_GetDepartmentName(t.EXAM_CATEGORY_NAME),
                            CASE
                                WHEN r.REPORT_RESULT LIKE '%阳性%' THEN '阳性'
                                WHEN r.REPORT_RESULT LIKE '%阴性%' THEN '阴性'
                                ELSE '未知'
                            END
                        ORDER BY 任务数量 DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", actualStartDate);
                        cmd.Parameters.AddWithValue("@EndDate", actualEndDate);
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
                dt.Columns.Add("错误", typeof(string));
                dt.Rows.Add(ex.Message);
            }

            return dt;
        }

        private DataTable GetMockDepartmentData()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("执行科室", typeof(string));
            dt.Columns.Add("结果状态", typeof(string));
            dt.Columns.Add("任务数量", typeof(int));
            dt.Columns.Add("阳性数量", typeof(int));
            dt.Columns.Add("阴性数量", typeof(int));
            dt.Columns.Add("阳性率", typeof(decimal));

            dt.Rows.Add("放射科", "阳性", 150, 45, 105, 30.0m);
            dt.Rows.Add("放射科", "阴性", 200, 10, 190, 5.0m);
            dt.Rows.Add("超声科", "阳性", 80, 20, 60, 25.0m);
            dt.Rows.Add("超声科", "阴性", 120, 8, 112, 6.67m);
            dt.Rows.Add("消化内镜(总)", "阳性", 60, 15, 45, 25.0m);

            return dt;
        }

        private DataTable GetMockDoctorData(string doctorType)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("医生姓名", typeof(string));
            dt.Columns.Add("医生类型", typeof(string));
            dt.Columns.Add("结果状态", typeof(string));
            dt.Columns.Add("任务数量", typeof(int));
            dt.Columns.Add("阳性数量", typeof(int));
            dt.Columns.Add("阴性数量", typeof(int));
            dt.Columns.Add("阳性率", typeof(decimal));

            string type = doctorType == "reviewer" ? "审核医生" : "报告医生";
            if (doctorType == "reviewer")
            {
                dt.Rows.Add("赵医生", type, "阳性", 80, 25, 55, 31.25m);
                dt.Rows.Add("赵医生", type, "阴性", 40, 3, 37, 7.5m);
                dt.Rows.Add("钱医生", type, "阳性", 60, 18, 42, 30.0m);
            }
            else
            {
                dt.Rows.Add("张医生", type, "阳性", 100, 30, 70, 30.0m);
                dt.Rows.Add("李医生", type, "阴性", 80, 5, 75, 6.25m);
                dt.Rows.Add("王医生", type, "阳性", 70, 21, 49, 30.0m);
            }

            return dt;
        }

        private DataTable GetMockCategoryData()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("检查类型", typeof(string));
            dt.Columns.Add("所属科室", typeof(string));
            dt.Columns.Add("结果状态", typeof(string));
            dt.Columns.Add("任务数量", typeof(int));
            dt.Columns.Add("阳性数量", typeof(int));
            dt.Columns.Add("阴性数量", typeof(int));
            dt.Columns.Add("阳性率", typeof(decimal));

            dt.Rows.Add("CT", "放射科", "阳性", 80, 24, 56, 30.0m);
            dt.Rows.Add("CT", "放射科", "阴性", 100, 5, 95, 5.0m);
            dt.Rows.Add("核磁共振", "放射科", "阳性", 40, 12, 28, 30.0m);
            dt.Rows.Add("超声", "超声科", "阳性", 60, 15, 45, 25.0m);
            dt.Rows.Add("胃镜(新)", "消化内镜(总)", "阳性", 30, 8, 22, 26.67m);

            return dt;
        }

        public string GetSystemTypes()
        {
            try
            {
                if (!ConnectionStrings.HasConnectionString())
                {
                    return "{\"success\": true, \"data\": [{\"code\": \"RIS\", \"name\": \"RIS（放射）\"}, {\"code\": \"UIS\", \"name\": \"UIS（超声）\"}, {\"code\": \"EIS\", \"name\": \"EIS（内镜）\"}]}";
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection())
                {
                    string sql = @"SELECT DISTINCT SYSTEM_SOURCE_NO AS code,
                        SYSTEM_SOURCE_NO + '（系统）' AS name
                        FROM EXAM_TASK WHERE IS_DEL = 0 AND SYSTEM_SOURCE_NO IS NOT NULL
                        ORDER BY SYSTEM_SOURCE_NO";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var systems = new List<Dictionary<string, string>>();
                        while (reader.Read())
                        {
                            systems.Add(new Dictionary<string, string>
                            {
                                { "code", reader["code"].ToString() },
                                { "name", reader["name"].ToString() }
                            });
                        }

                        if (systems.Count == 0)
                        {
                            systems.Add(new Dictionary<string, string> { { "code", "RIS" }, { "name", "RIS（放射）" } });
                            systems.Add(new Dictionary<string, string> { { "code", "UIS" }, { "name", "UIS（超声）" } });
                            systems.Add(new Dictionary<string, string> { { "code", "EIS" }, { "name", "EIS（内镜）" } });
                        }

                        return string.Format("{{\"success\": true, \"data\": {0}}}",
                            Newtonsoft.Json.JsonConvert.SerializeObject(systems));
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取系统类型失败");
                return "{\"success\": true, \"data\": [{\"code\": \"RIS\", \"name\": \"RIS（放射）\"}, {\"code\": \"UIS\", \"name\": \"UIS（超声）\"}, {\"code\": \"EIS\", \"name\": \"EIS（内镜）\"}]}";
            }
        }

        public string GetReporters(string system = "")
        {
            try
            {
                if (!ConnectionStrings.HasConnectionString())
                {
                    return GetDefaultReportersJson();
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection())
                {
                    string sql = @"SELECT DISTINCT WRITER_NAME AS code, WRITER_NAME AS name
                        FROM EXAM_REPORT
                        WHERE WRITER_NAME IS NOT NULL AND WRITER_NAME != ''
                        AND (@System IS NULL OR @System = '' OR SYSTEM_SOURCE_NO = @System)
                        ORDER BY WRITER_NAME";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@System", system ?? "");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var reporters = new List<Dictionary<string, string>>();
                            while (reader.Read())
                            {
                                reporters.Add(new Dictionary<string, string>
                                {
                                    { "code", reader["code"].ToString() },
                                    { "name", reader["name"].ToString() }
                                });
                            }

                            if (reporters.Count == 0)
                            {
                                return GetDefaultReportersJson();
                            }

                            return string.Format("{{\"success\": true, \"data\": {0}}}",
                                Newtonsoft.Json.JsonConvert.SerializeObject(reporters));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取报告医生失败");
                return GetDefaultReportersJson();
            }
        }

        public string GetReviewers(string system = "")
        {
            try
            {
                if (!ConnectionStrings.HasConnectionString())
                {
                    return GetDefaultReviewersJson();
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection())
                {
                    string sql = @"SELECT DISTINCT REVIEWER_NAME AS code, REVIEWER_NAME AS name
                        FROM EXAM_REPORT
                        WHERE REVIEWER_NAME IS NOT NULL AND REVIEWER_NAME != ''
                        AND (@System IS NULL OR @System = '' OR SYSTEM_SOURCE_NO = @System)
                        ORDER BY REVIEWER_NAME";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@System", system ?? "");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var reviewers = new List<Dictionary<string, string>>();
                            while (reader.Read())
                            {
                                reviewers.Add(new Dictionary<string, string>
                                {
                                    { "code", reader["code"].ToString() },
                                    { "name", reader["name"].ToString() }
                                });
                            }

                            if (reviewers.Count == 0)
                            {
                                return GetDefaultReviewersJson();
                            }

                            return string.Format("{{\"success\": true, \"data\": {0}}}",
                                Newtonsoft.Json.JsonConvert.SerializeObject(reviewers));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取审核医生失败");
                return GetDefaultReviewersJson();
            }
        }

        public string GetDepartments(string system = "")
        {
            try
            {
                if (!ConnectionStrings.HasConnectionString())
                {
                    return GetDefaultDepartmentsJson();
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection())
                {
                    string sql;
                    if (string.IsNullOrEmpty(system))
                    {
                        sql = "SELECT DISTINCT DepartmentName as code, DepartmentName as name FROM DEPT_CATEGORY_MAPPING";
                    }
                    else
                    {
                        sql = @"SELECT DISTINCT d.DepartmentName as code, d.DepartmentName as name
                            FROM DEPT_CATEGORY_MAPPING d
                            JOIN EXAM_TASK t ON d.CategoryName = t.EXAM_CATEGORY_NAME
                            WHERE t.SYSTEM_SOURCE_NO = @System";
                    }

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        if (!string.IsNullOrEmpty(system))
                        {
                            cmd.Parameters.AddWithValue("@System", system);
                        }

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var departments = new List<Dictionary<string, string>>();
                            while (reader.Read())
                            {
                                departments.Add(new Dictionary<string, string>
                                {
                                    { "code", reader["code"].ToString() },
                                    { "name", reader["name"].ToString() }
                                });
                            }

                            if (departments.Count == 0)
                            {
                                return GetDefaultDepartmentsJson();
                            }

                            return string.Format("{{\"success\": true, \"data\": {0}}}",
                                Newtonsoft.Json.JsonConvert.SerializeObject(departments));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取执行科室失败");
                return GetDefaultDepartmentsJson();
            }
        }

        public string GetPatientTypes(string system = "")
        {
            try
            {
                if (!ConnectionStrings.HasConnectionString())
                {
                    return GetDefaultPatientTypesJson();
                }

                using (SqlConnection conn = DatabaseConnection.GetConnection())
                {
                    string sql = @"SELECT DISTINCT PATIENT_TYPE as code, PATIENT_TYPE as name
                        FROM EXAM_TASK_INFO
                        WHERE PATIENT_TYPE IS NOT NULL AND PATIENT_TYPE != ''
                        AND (@System IS NULL OR @System = '' OR SYSTEM_SOURCE_NO = @System)
                        ORDER BY PATIENT_TYPE";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@System", system ?? "");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var patientTypes = new List<Dictionary<string, string>>();
                            while (reader.Read())
                            {
                                patientTypes.Add(new Dictionary<string, string>
                                {
                                    { "code", reader["code"].ToString() },
                                    { "name", reader["name"].ToString() }
                                });
                            }

                            if (patientTypes.Count == 0)
                            {
                                return GetDefaultPatientTypesJson();
                            }

                            return string.Format("{{\"success\": true, \"data\": {0}}}",
                                Newtonsoft.Json.JsonConvert.SerializeObject(patientTypes));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取病人类型失败");
                return GetDefaultPatientTypesJson();
            }
        }

        public string GetResultStatusTypes()
        {
            return "{\"success\": true, \"data\": [{\"code\": \"阳性\", \"name\": \"阳性\"}, {\"code\": \"阴性\", \"name\": \"阴性\"}, {\"code\": \"未知\", \"name\": \"未知\"}]}";
        }

        private string GetDefaultReportersJson()
        {
            return "{\"success\": true, \"data\": [{\"code\": \"张医生\", \"name\": \"张医生\"}, {\"code\": \"李医生\", \"name\": \"李医生\"}, {\"code\": \"王医生\", \"name\": \"王医生\"}, {\"code\": \"赵医生\", \"name\": \"赵医生\"}]}";
        }

        private string GetDefaultReviewersJson()
        {
            return "{\"success\": true, \"data\": [{\"code\": \"赵医生\", \"name\": \"赵医生\"}, {\"code\": \"钱医生\", \"name\": \"钱医生\"}, {\"code\": \"孙医生\", \"name\": \"孙医生\"}, {\"code\": \"周医生\", \"name\": \"周医生\"}]}";
        }

        private string GetDefaultDepartmentsJson()
        {
            return "{\"success\": true, \"data\": [{\"code\": \"消化内镜(总)\", \"name\": \"消化内镜(总)\"}, {\"code\": \"呼吸内镜科\", \"name\": \"呼吸内镜科\"}, {\"code\": \"放射科\", \"name\": \"放射科\"}, {\"code\": \"超声科\", \"name\": \"超声科\"}]}";
        }

        private string GetDefaultPatientTypesJson()
        {
            return "{\"success\": true, \"data\": [{\"code\": \"门诊\", \"name\": \"门诊\"}, {\"code\": \"住院\", \"name\": \"住院\"}, {\"code\": \"急诊\", \"name\": \"急诊\"}, {\"code\": \"体检\", \"name\": \"体检\"}]}";
        }

        public string GetHospitalName()
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogInfo("数据库配置为空，使用默认医院名称");
                return "XX医院";
            }

            LogHelper.LogInfo("尝试从数据库获取医院名称...");

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    LogHelper.LogInfo("数据库连接成功");

                    string sql = @"SELECT TOP 1 HOSPITAL_NAME FROM SYS_CONFIG WHERE CONFIG_KEY = 'HOSPITAL_NAME'";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            string hospitalName = result.ToString();
                            LogHelper.LogInfo($"从SYS_CONFIG获取医院名称: {hospitalName}");
                            return hospitalName;
                        }
                        LogHelper.LogInfo("SYS_CONFIG表中未找到医院名称配置");
                    }

                    sql = @"SELECT TOP 1 HOSPITAL_NAME FROM EXAM_TASK WHERE HOSPITAL_NAME IS NOT NULL";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            string hospitalName = result.ToString();
                            LogHelper.LogInfo($"从EXAM_TASK获取医院名称: {hospitalName}");
                            return hospitalName;
                        }
                        LogHelper.LogInfo("EXAM_TASK表中未找到医院名称配置");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取医院名称失败");
                LogHelper.LogError($"数据库连接错误: {ex.Message}");
            }

            LogHelper.LogInfo("未找到医院名称配置，使用默认值");
            return "XX医院";
        }
    }
}
