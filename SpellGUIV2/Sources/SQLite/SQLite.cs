﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpellEditor.Sources.Config;
using System.Reflection;

namespace SpellEditor.Sources.SQLite
{
	class SQLite
	{
		 private Config.Config config;
        private SQLiteConnection conn = null;
        public string Table;
        private bool _updating = false;

        public SQLite(Config.Config config)
        {
            this.config = config;
            this.Table = config.Table;

			string connectionString = "Data Source =" + Environment.CurrentDirectory + "\\SpellEditor.db"; ;

			conn = new SQLiteConnection(connectionString);
            //conn.ConnectionString = connectionString;
            conn.Open();
            // Create DB
            var cmd = conn.CreateCommand();
			/*
            cmd.CommandText = string.Format("CREATE DATABASE IF NOT EXISTS `{0}`;", config.Database);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
            cmd = conn.CreateCommand();
            cmd.CommandText = string.Format("USE `{0}`;", config.Database);
            cmd.ExecuteNonQuery();
            cmd.Dispose();*/
            // Create Table
            //cmd = conn.CreateCommand();

            cmd.CommandText = string.Format(getTableCreateString(), config.Table);
            cmd.ExecuteNonQuery();
            cmd.Dispose();
        }

        ~SQLite()
        {
            if (conn != null)
                conn.Close();
        }

        private readonly object syncLock = new object();

        public DataTable query(string query)
        {
            lock (syncLock)
            {
                var adapter = new SQLiteDataAdapter(query, conn);
                DataSet DS = new DataSet();
                adapter.SelectCommand.CommandTimeout = 0;
                adapter.Fill(DS);
                return DS.Tables[0];
            }
        }

        public void commitChanges(string query, DataTable dataTable)
        {
            if (_updating)
                return;
            lock (syncLock)
            {
                var adapter = new SQLiteDataAdapter();
                var mcb = new SQLiteCommandBuilder(adapter);
                mcb.ConflictOption = ConflictOption.OverwriteChanges;
                adapter.SelectCommand = new SQLiteCommand(query, conn);
                adapter.Update(dataTable);
                dataTable.AcceptChanges();
            }
        }

        public void execute(string p)
        {
            if (_updating)
                return;
            //lock (syncLock)
            //{
                var cmd = conn.CreateCommand();
                cmd.CommandText = p;
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            //}
        }

        private string getTableCreateString()
        {
            StringBuilder str = new StringBuilder();
            str.Append(@"CREATE TABLE IF NOT EXISTS `{0}` (");

            var structure = new SpellEditor.Sources.DBC.Spell_DBC_Record();
            var fields = structure.GetType().GetFields();
            foreach (var f in fields)
            {
                switch (Type.GetTypeCode(f.FieldType))
                {
                    case TypeCode.UInt32:
                        {
							str.Append(string.Format(@"`{0}` INTEGER(10) NOT NULL DEFAULT '0', ", f.Name));
                            break;
                        }
                    case TypeCode.Int32:
						str.Append(string.Format(@"`{0}` INTEGER(11) NOT NULL DEFAULT '0', ", f.Name));
                        break;
                    case TypeCode.Single:
						str.Append(string.Format(@"`{0}` REAL NOT NULL DEFAULT '0', ", f.Name));
                        break;
                    case TypeCode.Object:
                        {
                            var attr = f.GetCustomAttribute<SpellEditor.Sources.DBC.HandleField>();
                            if (attr != null)
                            {
                                if (attr.Method == 1)
                                {
                                    for (int i = 0; i < attr.Count; ++i)
										str.Append(string.Format(@"`{0}{1}` TEXT, ", f.Name, i));
                                    break;
                                }
                                else if (attr.Method == 2)
                                {
                                    for (int i = 0; i < attr.Count; ++i)
										str.Append(string.Format(@"`{0}{1}` INTEGER(10) NOT NULL DEFAULT '0', ", f.Name, i));
                                    break;
                                }
                            }
                            goto default;
                        }
                    default:
                        throw new Exception("ERROR: Unhandled type: " + f.FieldType + " on field: " + f.Name);

                }
            }

            str.Append(@"PRIMARY KEY (`ID`));");
            
            return str.ToString();
        }

        public void setUpdating(bool p)
        {
            _updating = p;
        }
    }
}
