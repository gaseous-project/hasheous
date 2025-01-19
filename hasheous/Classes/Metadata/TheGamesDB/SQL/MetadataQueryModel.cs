namespace TheGamesDB.SQL
{
    public class QueryModel
    {
        public string? query { get; set; }
        private QueryFieldName? _queryField { get; set; } = QueryFieldName.id;
        public QueryFieldName? queryField
        {
            get
            {
                if (_queryField == null)
                {
                    return QueryFieldName.id;
                }
                else
                {
                    return _queryField;
                }
            }
            set
            {
                _queryField = value;
            }
        }
        public string? fieldList { get; set; }
        public string? includeList { get; set; }
        public string? filter { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }
        public enum QueryFieldName
        {
            id,
            name,
            platform_id,
            platforms_id,
            platform_name,
            games_id
        }
    }

    public class QueryValidator
    {
        public QueryModel queryModel { get; set; }

        public List<string> validFieldList { get; set; }
        public List<string> baseFieldList { get; set; }
        public List<string> validIncludeList { get; set; }
        public List<string> validFilterList { get; set; }

        public QueryValidator(QueryModel queryModel)
        {
            this.queryModel = queryModel;
        }

        public QueryValidator(QueryModel queryModel, List<string> validFieldList, List<string> baseFieldList, List<string> validIncludeList, List<string> validFilterList)
        {
            this.queryModel = queryModel;
            this.validFieldList = validFieldList;
            this.validIncludeList = validIncludeList;
            this.validFilterList = validFilterList;
        }

        public List<string> queryItems
        {
            get
            {
                switch (queryModel.queryField)
                {
                    case QueryModel.QueryFieldName.id:
                    case QueryModel.QueryFieldName.platform_id:
                    case QueryModel.QueryFieldName.platforms_id:
                    case QueryModel.QueryFieldName.games_id:
                        // split query by comma and trim each item
                        return queryModel.query.Split(',').Select(s => s.Trim()).ToList();

                    case QueryModel.QueryFieldName.name:
                    case QueryModel.QueryFieldName.platform_name:
                        // return only the name
                        return new List<string> { queryModel.query.Trim() };

                    default:
                        return new List<string>();

                }
            }
        }

        public List<string> fieldItems
        {
            get
            {
                // split, trim and toLower fieldList by comma, then verify each item is in validFieldList
                if (queryModel.fieldList == null)
                {
                    return baseFieldList;
                }

                List<string> fields = [.. baseFieldList];

                List<string> verifiedFields = queryModel.fieldList.ToLower().Split(',').Select(s => s.Trim()).Where(s => validFieldList.Contains(s)).ToList();

                fields.AddRange(verifiedFields);

                return fields;
            }
        }

        public List<string> filterItems
        {
            get
            {
                // split, trim and toLower filter by comma, then verify each item is in validFilterList
                if (queryModel.filter == null)
                {
                    return validFilterList;
                }

                if (queryModel.filter.Contains("*"))
                {
                    return validFieldList;
                }

                return queryModel.filter.ToLower().Split(',').Select(s => s.Trim()).Where(s => validFilterList.Contains(s)).ToList();
            }
        }

        public string sqlWhereClause
        {
            get
            {
                string inClause = "";

                switch (queryModel.queryField)
                {
                    case QueryModel.QueryFieldName.id:
                        if (queryItems.Count == 0)
                        {
                            return "";
                        }

                        if (queryItems.Count == 1)
                        {
                            return "`id` = @id0";
                        }

                        for (int i = 0; i < queryItems.Count; i++)
                        {
                            if (inClause.Length > 0)
                            {
                                inClause += ", ";
                            }
                            inClause += "@id" + i;
                        }

                        return queryItems.Count > 0 ? $"`id` IN ({inClause})" : "";

                    case QueryModel.QueryFieldName.platform_id:
                        if (queryItems.Count == 0)
                        {
                            return "";
                        }

                        if (queryItems.Count == 1)
                        {
                            return "`platform` = @id0";
                        }

                        for (int i = 0; i < queryItems.Count; i++)
                        {
                            if (inClause.Length > 0)
                            {
                                inClause += ", ";
                            }
                            inClause += "@id" + i;
                        }

                        return queryItems.Count > 0 ? $"`platform` IN ({inClause})" : "";

                    case QueryModel.QueryFieldName.platforms_id:
                        if (queryItems.Count == 0)
                        {
                            return "";
                        }

                        if (queryItems.Count == 1)
                        {
                            return "`platforms_id` = @id0";
                        }

                        for (int i = 0; i < queryItems.Count; i++)
                        {
                            if (inClause.Length > 0)
                            {
                                inClause += ", ";
                            }
                            inClause += "@id" + i;
                        }

                        return queryItems.Count > 0 ? $"`platforms_id` IN ({inClause})" : "";

                    case QueryModel.QueryFieldName.platform_name:
                        return queryItems.Count > 0 ? $"`name` LIKE @id0" : "";

                    case QueryModel.QueryFieldName.name:
                        return queryItems.Count > 0 ? $"`game_title` LIKE @id0" : "";

                    case QueryModel.QueryFieldName.games_id:
                        return queryItems.Count > 0 ? $"`games_id` = @id0" : "";

                    default:
                        return "";
                }
            }
        }

        public Dictionary<string, object> sqlWhereClauseValues
        {
            get
            {
                Dictionary<string, object> fields = new Dictionary<string, object>();

                for (int i = 0; i < queryItems.Count; i++)
                {
                    fields.Add("@id" + i, queryItems[i]);
                }

                return fields;
            }
        }

        public List<string> includeItems
        {
            get
            {
                // split, trim and toLower includeList by comma, then verify each item is in validIncludeList
                if (queryModel.includeList == null)
                {
                    return new List<string>();
                }

                if (queryModel.includeList.Contains("*"))
                {
                    return validIncludeList;
                }

                return queryModel.includeList.ToLower().Split(',').Select(s => s.Trim()).Where(s => validIncludeList.Contains(s)).ToList();
            }
        }
    }
}