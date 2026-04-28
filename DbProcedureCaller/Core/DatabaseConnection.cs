using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace DbProcedureCaller.Core
{
    public static class DatabaseConnection
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, SqlConnection> _connectionPool = new Dictionary<string, SqlConnection>();

        public static SqlConnection GetConnection(string connectionString)
        {
            lock (_lock)
            {
                if (_connectionPool.ContainsKey(connectionString))
                {
                    SqlConnection conn = _connectionPool[connectionString];
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        return conn;
                    }
                    else
                    {
                        _connectionPool.Remove(connectionString);
                    }
                }

                SqlConnection newConn = new SqlConnection(connectionString);
                newConn.Open();
                _connectionPool[connectionString] = newConn;
                return newConn;
            }
        }

        public static SqlConnection GetConnection()
        {
            string connectionString = Config.ConnectionStrings.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("未配置数据库连接字符串");
            }
            return GetConnection(connectionString);
        }

        public static void CloseConnection(string connectionString)
        {
            lock (_lock)
            {
                if (_connectionPool.ContainsKey(connectionString))
                {
                    SqlConnection conn = _connectionPool[connectionString];
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        conn.Close();
                    }
                    _connectionPool.Remove(connectionString);
                }
            }
        }

        public static void CloseAllConnections()
        {
            lock (_lock)
            {
                foreach (var conn in _connectionPool.Values)
                {
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        conn.Close();
                    }
                }
                _connectionPool.Clear();
            }
        }

        public static bool TestConnection(string connectionString, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TestConnection(out string errorMessage)
        {
            string connectionString = Config.ConnectionStrings.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                errorMessage = "未配置数据库连接字符串";
                return false;
            }
            return TestConnection(connectionString, out errorMessage);
        }
    }
}