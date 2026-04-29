using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;
using DbProcedureCaller.Core;
using DbProcedureCaller.Config;

namespace DbProcedureCaller.Services
{
    public class UserService
    {
        private static readonly object _lock = new object();

        public bool ValidateUser(string username, string password)
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空，无法验证用户");
                return false;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string sql = "SELECT COUNT(*) FROM TJYHB WHERE YHM = @username AND YKL = @password AND SFY = 1";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", password);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "验证用户失败");
                MessageBox.Show($"数据库连接失败，请检查数据库配置或联系管理员处理。错误信息: {ex.Message}",
                    "数据库连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void EnsureTableExists(SqlConnection conn)
        {
            try
            {
                string checkTableSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'TJYHB'";
                using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn))
                {
                    int tableExists = (int)checkCmd.ExecuteScalar();
                    
                    if (tableExists == 0)
                    {
                        LogHelper.LogInfo("TJYHB表不存在，正在创建...");
                        
                        string createTableSql = @"
                            CREATE TABLE TJYHB (
                                ID INT PRIMARY KEY,
                                YHM VARCHAR(50) NOT NULL UNIQUE,
                                YKL VARCHAR(100) NOT NULL,
                                QX INT DEFAULT 0,
                                SFY INT DEFAULT 1
                            )";
                        using (SqlCommand createCmd = new SqlCommand(createTableSql, conn))
                        {
                            createCmd.ExecuteNonQuery();
                            LogHelper.LogInfo("TJYHB表创建成功");
                        }

                        string insertDefaultUserSql = "INSERT INTO TJYHB (ID, YHM, YKL, QX, SFY) VALUES (1, 'lhbdb', '241023', 1, 1)";
                        using (SqlCommand insertCmd = new SqlCommand(insertDefaultUserSql, conn))
                        {
                            insertCmd.ExecuteNonQuery();
                            LogHelper.LogInfo("默认管理员用户创建成功");
                        }
                    }
                    else
                    {
                        LogHelper.LogInfo("TJYHB表已存在，检查并添加缺失的列...");
                        
                        AddMissingColumn(conn, "YHM", "VARCHAR(50) NOT NULL");
                        AddMissingColumn(conn, "YKL", "VARCHAR(100) NOT NULL");
                        AddMissingColumn(conn, "QX", "INT DEFAULT 0");
                        AddMissingColumn(conn, "SFY", "INT DEFAULT 1");
                        
                        LogHelper.LogInfo("TJYHB表结构检查完成");
                        
                        string checkUniqueSql = "SELECT COUNT(*) FROM sys.indexes WHERE name = 'UQ_TJYHB_YHM' AND object_id = OBJECT_ID('TJYHB')";
                        using (SqlCommand checkUniqueCmd = new SqlCommand(checkUniqueSql, conn))
                        {
                            int hasUnique = (int)checkUniqueCmd.ExecuteScalar();
                            if (hasUnique == 0)
                            {
                                try
                                {
                                    string addUniqueSql = "ALTER TABLE TJYHB ADD CONSTRAINT UQ_TJYHB_YHM UNIQUE (YHM)";
                                    using (SqlCommand addUniqueCmd = new SqlCommand(addUniqueSql, conn))
                                    {
                                        addUniqueCmd.ExecuteNonQuery();
                                        LogHelper.LogInfo("已添加YHM唯一约束");
                                    }
                                }
                                catch { }
                            }
                        }
                        
                        string checkUserSql = "SELECT COUNT(*) FROM TJYHB WHERE YHM = 'lhbdb'";
                        using (SqlCommand checkUserCmd = new SqlCommand(checkUserSql, conn))
                        {
                            int userExists = (int)checkUserCmd.ExecuteScalar();
                            if (userExists == 0)
                            {
                                string insertDefaultUserSql = "INSERT INTO TJYHB (ID, YHM, YKL, QX, SFY) VALUES (1, 'lhbdb', '241023', 1, 1)";
                                using (SqlCommand insertCmd = new SqlCommand(insertDefaultUserSql, conn))
                                {
                                    try
                                    {
                                        insertCmd.ExecuteNonQuery();
                                        LogHelper.LogInfo("默认管理员用户不存在，已创建");
                                    }
                                    catch (Exception ex)
                                    {
                                        LogHelper.LogInfo($"插入默认用户失败（可能ID已存在）: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"初始化数据库表失败: {ex.Message}");
            }
        }

        private void AddMissingColumn(SqlConnection conn, string columnName, string columnType)
        {
            string checkColumnSql = @"
                SELECT COUNT(*) 
                FROM sys.columns 
                WHERE object_id = OBJECT_ID('TJYHB') 
                AND name = @columnName";
            using (SqlCommand cmd = new SqlCommand(checkColumnSql, conn))
            {
                cmd.Parameters.AddWithValue("@columnName", columnName);
                int count = (int)cmd.ExecuteScalar();
                if (count == 0)
                {
                    string addColumnSql = $"ALTER TABLE TJYHB ADD {columnName} {columnType}";
                    using (SqlCommand addCmd = new SqlCommand(addColumnSql, conn))
                    {
                        addCmd.ExecuteNonQuery();
                        LogHelper.LogInfo($"已添加缺失列: {columnName}");
                    }
                }
            }
        }

        public bool ValidateUserWithConnectionCheck(string username, string password)
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空，无法验证用户");
                MessageBox.Show("数据库连接未配置，请先配置数据库连接。", "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string sql = "SELECT COUNT(*) FROM TJYHB WHERE YHM = @username AND YKL = @password AND SFY = 1";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", password);
                        int count = (int)cmd.ExecuteScalar();
                        if (count > 0)
                        {
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("用户名或密码错误，请重试。", "登录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "验证用户失败");
                MessageBox.Show($"数据库连接失败，请检查数据库配置。错误信息: {ex.Message}",
                    "数据库连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public bool IsAdminUser(string username)
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空，无法检查管理员权限");
                return false;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string sql = "SELECT QX FROM TJYHB WHERE YHM = @username";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return (int)result == 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "检查管理员权限失败");
            }

            return false;
        }

        public string GetUsersJson()
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                LogHelper.LogError("数据库连接字符串为空，无法获取用户列表");
                return "{\"success\": false, \"error\": \"数据库连接未配置\", \"data\": []}";
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string sql = "SELECT ID, YHM as username, CASE WHEN QX = 1 THEN '管理员' ELSE '普通用户' END as role, CASE WHEN SFY = 1 THEN '启用' ELSE '禁用' END as status FROM TJYHB";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>> users = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                            while (reader.Read())
                            {
                                var user = new System.Collections.Generic.Dictionary<string, object>();
                                user["id"] = reader["ID"];
                                user["username"] = reader["username"];
                                user["role"] = reader["role"];
                                user["status"] = reader["status"];
                                users.Add(user);
                            }
                            return "{\"success\": true, \"data\": " + Newtonsoft.Json.JsonConvert.SerializeObject(users) + "}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "获取用户列表失败");
                return "{\"success\": false, \"error\": \"" + ex.Message + "\"}";
            }
        }

        public bool AddUser(int id, string username, string password, string role, string status)
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                return false;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string checkSql = "SELECT COUNT(*) FROM TJYHB WHERE ID = @id OR YHM = @username";
                    using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        checkCmd.Parameters.AddWithValue("@username", username);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            return false;
                        }
                    }

                    string sql = "INSERT INTO TJYHB (ID, YHM, YKL, QX, SFY) VALUES (@id, @username, @password, @role, @status)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", password);
                        cmd.Parameters.AddWithValue("@role", role == "管理员" ? 1 : 0);
                        cmd.Parameters.AddWithValue("@status", status == "启用" ? 1 : 0);
                        cmd.ExecuteNonQuery();
                        LogHelper.LogInfo($"用户添加成功: {username}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "添加用户失败");
                return false;
            }
        }

        public bool UpdateUser(int id, string username, string password, string role, string status)
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                return false;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string checkAdminSql = "SELECT YHM FROM TJYHB WHERE ID = @id";
                    using (SqlCommand checkCmd = new SqlCommand(checkAdminSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        object result = checkCmd.ExecuteScalar();
                        if (result != null && result.ToString() == AppConstants.DefaultAdminUser)
                        {
                            return false;
                        }
                    }

                    SqlCommand cmd;
                    string sql;

                    if (string.IsNullOrEmpty(password))
                    {
                        sql = "UPDATE TJYHB SET YHM = @username, QX = @role, SFY = @status WHERE ID = @id";
                        cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@role", role == "管理员" ? 1 : 0);
                        cmd.Parameters.AddWithValue("@status", status == "启用" ? 1 : 0);
                    }
                    else
                    {
                        sql = "UPDATE TJYHB SET YHM = @username, YKL = @password, QX = @role, SFY = @status WHERE ID = @id";
                        cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", password);
                        cmd.Parameters.AddWithValue("@role", role == "管理员" ? 1 : 0);
                        cmd.Parameters.AddWithValue("@status", status == "启用" ? 1 : 0);
                    }

                    int rows = cmd.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        LogHelper.LogInfo($"用户更新成功: ID={id}");
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "更新用户失败");
                return false;
            }
        }

        public bool DeleteUser(int id)
        {
            string connectionString = ConnectionStrings.GetConnectionString();

            if (string.IsNullOrEmpty(connectionString))
            {
                return false;
            }

            try
            {
                using (SqlConnection conn = DatabaseConnection.GetConnection(connectionString))
                {
                    EnsureTableExists(conn);
                    
                    string checkSql = "SELECT YHM FROM TJYHB WHERE ID = @id";
                    using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        object result = checkCmd.ExecuteScalar();
                        if (result != null && result.ToString() == AppConstants.DefaultAdminUser)
                        {
                            return false;
                        }
                    }

                    string sql = "DELETE FROM TJYHB WHERE ID = @id";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            LogHelper.LogInfo($"用户删除成功: ID={id}");
                            return true;
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "删除用户失败");
                return false;
            }
        }
    }
}
