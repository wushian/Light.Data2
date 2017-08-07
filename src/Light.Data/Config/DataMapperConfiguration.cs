﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Light.Data
{
    public static class DataMapperConfiguration
    {
        static object locker = new object();

        static HashSet<string> configFilePaths = new HashSet<string>();

        static bool initialed;

        static Dictionary<Type, DataTableMapperSetting> settingDict = new Dictionary<Type, DataTableMapperSetting>();
        
        public static void AddConfigFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (initialed) {
                throw new LightDataException(SR.ConfigurationHasBeenInitialized);
            }
            configFilePaths.Add(filePath);
        }

        static void Initial()
        {
            if (configFilePaths.Count == 0) {
                configFilePaths.Add("lightdata.json");
            }
            foreach (var file in configFilePaths) {
                LoadData(file);
            }
            initialed = true;
        }

        static void LoadData(string configFilePath)
        {
            FileInfo fileInfo = new FileInfo(configFilePath);
            if (fileInfo.Exists) {
                using (StreamReader reader = fileInfo.OpenText()) {
                    string content = reader.ReadToEnd();
                    JObject dom = JObject.Parse(content);
                    var section = dom.GetValue("lightDataMapper");
                    if (section == null) {
                        return;
                    }
                    var optionList = section.ToObject<LightMapperOptions>();
                    if (optionList != null && optionList.DataTypes != null && optionList.DataTypes.Length > 0) {
                        int typeIndex = 0;
                        foreach (DataTypeSection typeConfig in optionList.DataTypes) {
                            typeIndex++;
                            var typeName = typeConfig.Type;
                            if (typeName == null) {
                                throw new LightDataException(string.Format(SR.ConfigDataTypeNameIsNull, typeIndex));
                            }
                            var dataType = Type.GetType(typeName, true);
                            var dataTypeInfo = dataType.GetTypeInfo();
                            var dataTableMap = new DataTableMapperConfig(dataType);
                            var setting = new DataTableMapperSetting(dataTableMap);

                            dataTableMap.TableName = typeConfig.TableName;
                            dataTableMap.IsEntityTable = typeConfig.IsEntityTable;
                            var configParam = new ConfigParamSet();
                            var paramConfigs = typeConfig.ConfigParams;
                            if (paramConfigs != null && paramConfigs.Length > 0) {
                                foreach (ConfigParamSection paramConfig in paramConfigs) {
                                    configParam.SetParamValue(paramConfig.Name, paramConfig.Value);
                                }
                            }
                            dataTableMap.ConfigParams = configParam;
                            var dataFieldConfigs = typeConfig.DataFields;

                            if (dataFieldConfigs != null && dataFieldConfigs.Length > 0) {
                                int fieldIndex = 0;
                                foreach (var fieldConfig in dataFieldConfigs) {
                                    fieldIndex++;
                                    var fieldName = fieldConfig.FieldName;
                                    if (fieldName == null) {
                                        throw new LightDataException(string.Format(SR.ConfigDataFieldNameIsNull, typeName, fieldIndex));
                                    }
                                    var property = dataTypeInfo.GetProperty(fieldName);
                                    if (property == null) {
                                        throw new LightDataException(string.Format(SR.ConfigDataFieldIsNotExists, typeName, fieldName));
                                    }

                                    object defaultValue;
                                    try {
                                        defaultValue = GreateDefaultValue(property.PropertyType, fieldConfig.DefaultValue);
                                    }
                                    catch (Exception ex) {
                                        throw new LightDataException(string.Format(SR.ConfigDataFieldLoadError, typeName, fieldName, ex.Message));
                                    }

                                    var dataFieldMap = new DataFieldMapperConfig(fieldName) {
                                        Name = fieldConfig.Name,
                                        IsPrimaryKey = fieldConfig.IsPrimaryKey,
                                        IsIdentity = fieldConfig.IsIdentity,
                                        DbType = fieldConfig.DbType,
                                        DataOrder = fieldConfig.DataOrder,
                                        IsNullable = fieldConfig.IsNullable,
                                        DefaultValue = defaultValue
                                    };
                                    setting.AddDataFieldMapConfig(fieldName, dataFieldMap);
                                }
                            }
                            var relationFieldConfigs = typeConfig.RelationFields;
                            if (relationFieldConfigs != null && relationFieldConfigs.Length > 0) {
                                int fieldIndex = 0;
                                foreach (var fieldConfig in relationFieldConfigs) {
                                    fieldIndex++;
                                    if (fieldConfig.RelationPairs != null && fieldConfig.RelationPairs.Length > 0) {
                                        var fieldName = fieldConfig.FieldName;
                                        if (fieldName == null) {
                                            throw new LightDataException(string.Format(SR.ConfigDataFieldNameIsNull, typeName, fieldIndex));
                                        }
                                        var property = dataTypeInfo.GetProperty(fieldName);
                                        if (property == null) {
                                            throw new LightDataException(string.Format(SR.ConfigDataFieldIsNotExists, typeName, fieldName));
                                        }
                                        var dataFieldMap = new RelationFieldMapConfig(fieldName);
                                        foreach (var pair in fieldConfig.RelationPairs) {
                                            dataFieldMap.AddRelationKeys(pair.MasterKey, pair.RelateKey);
                                        }
                                        setting.AddRelationFieldMapConfig(fieldName, dataFieldMap);
                                    }
                                }
                            }
                            settingDict[dataType] = setting;
                        }
                    }
                }
            }
        }

        private static object GreateDefaultValue(Type type, string defaultValue)
        {
            if (!string.IsNullOrEmpty(defaultValue)) {
                TypeInfo typeInfo = type.GetTypeInfo();
                if (typeInfo.IsGenericType) {
                    Type frameType = type.GetGenericTypeDefinition();
                    if (frameType.FullName == "System.Nullable`1") {
                        Type[] arguments = typeInfo.GetGenericArguments();
                        type = arguments[0];
                        typeInfo = type.GetTypeInfo();
                    }
                }
                object valueObj;
                if (type == typeof(string)) {
                    valueObj = defaultValue;
                }
                else if (typeInfo.IsEnum) {
                    int index = defaultValue.LastIndexOf(".");
                    if (index > 0) {
                        string typeName = defaultValue.Substring(0, index);
                        if (typeName != type.Name) {
                            throw new Exception(string.Format("\"{0}\" is not correct type", defaultValue));
                        }
                        defaultValue = defaultValue.Substring(index + 1);
                    }
                    valueObj = Enum.Parse(type, defaultValue, true);
                }
                else {
                    if (type == typeof(DateTime)) {
                        if (DateTime.TryParse(defaultValue, out DateTime dt)) {
                            valueObj = dt;
                        }
                        else {
                            int index = defaultValue.LastIndexOf(".");
                            if (index > 0) {
                                string typeName = defaultValue.Substring(0, index);
                                if (typeName != "DefaultTime") {
                                    throw new Exception(string.Format("\"{0}\" is not correct type", defaultValue));
                                }
                                defaultValue = defaultValue.Substring(index + 1);

                            }
                            valueObj = Enum.Parse(typeof(DefaultTime), defaultValue, true);
                        }
                    }
                    else {
                        valueObj = Convert.ChangeType(defaultValue, type);
                    }
                }
                return valueObj;
            }
            else {
                return null;
            }
        }

        private static void CheckData()
        {
            if (!initialed) {
                lock (locker) {
                    if (!initialed) {
                        Initial();
                    }
                }
            }
        }

        internal static bool TryGetSetting(Type type, out DataTableMapperSetting setting)
        {
            CheckData();
            return settingDict.TryGetValue(type, out setting);
        }
    }
}
