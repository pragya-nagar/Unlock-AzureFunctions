using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;

using System.Text;

namespace AzureFunctions.Repository
{
    public class BaseRepository
    {
        private SqlConnection _connection;
        public string ConnAdmin { get; set; }
        public string ConnNotifications { get; set; }
        public string ConnOkrService { get; set; }

        public BaseRepository(IConfiguration configuration)
        {
            //ConnAdmin = Environment.GetEnvironmentVariable("SqlConnectionStringAdmin", EnvironmentVariableTarget.Process);
            //ConnNotifications = Environment.GetEnvironmentVariable("SqlConnectionStringNotification", EnvironmentVariableTarget.Process);
            //ConnOkrService = Environment.GetEnvironmentVariable("SqlConnectionStringOkr", EnvironmentVariableTarget.Process);

            ConnAdmin = configuration.GetConnectionString("SqlConnectionStringAdmin");
            ConnNotifications = configuration.GetConnectionString("SqlConnectionStringNotification");
            ConnOkrService = configuration.GetConnectionString("SqlConnectionStringOkr");

        }


        /// <summary>
        /// Establish the database connection with Admin service database
        /// </summary>
        public SqlConnection DbConnectionAdmin
        {
            get
            {
                try
                {
                    if (_connection == null && !string.IsNullOrEmpty(ConnAdmin))
                    {
                        _connection = new SqlConnection(ConnAdmin);
                    }
                    if (_connection != null && _connection.State != ConnectionState.Open)
                    {
                        _connection.Open();
                    }
                }
                catch (Exception)
                {
                    _connection = new SqlConnection(ConnAdmin);
                }

                return _connection;
            }
        }

        /// <summary>
        /// Establish the database connection with Notifications database
        /// </summary>
        public SqlConnection DbConnectionNotifications
        {
            get
            {
                try
                {
                    if (_connection == null && !string.IsNullOrEmpty(ConnNotifications))
                    {
                        _connection = new SqlConnection(ConnNotifications);
                    }
                    if (_connection != null && _connection.State != ConnectionState.Open)
                    {
                        _connection.Open();
                    }
                }
                catch (Exception)
                {
                    _connection = new SqlConnection(ConnNotifications);
                }

                return _connection;
            }
        }

        /// <summary>
        /// Establish the database connection with OkrService database
        /// </summary>
        public SqlConnection DbConnectionOkrService
        {
            get
            {
                try
                {
                    if (_connection == null && !string.IsNullOrEmpty(ConnOkrService))
                    {
                        _connection = new SqlConnection(ConnOkrService);
                    }
                    if (_connection != null && _connection.State != ConnectionState.Open)
                    {
                        _connection.Open();
                    }
                }
                catch (Exception)
                {
                    _connection = new SqlConnection(ConnOkrService);
                }

                return _connection;
            }
        }
    }
}
