﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LER2
{
    public class Ler2MergeValidator
    {
        private Dictionary<string, MergeRules> _mergeRules = new Dictionary<string, MergeRules>();
        public void LoadRule(string ler2Type, string basePath)
        {
            string csvPath = Path.Combine(basePath, $"{ler2Type}.csv");
            if (!File.Exists(csvPath))
                throw new Exception($"Merge rule for Ler2Type {ler2Type} does not exist!");

            MergeRules rules = new MergeRules();

            foreach (string line in File.ReadAllLines(csvPath).Skip(1))
            {
                if (line.Trim() == "") continue;
                var parts = line.Split(';');
                if (parts.Length != 2) throw new Exception($"Invalid rule: {line} in {csvPath}!");
                MergeRuleType rule;
                if (Enum.TryParse(parts[1], out rule))
                {
                    rules.PropertyRules.Add(parts[0], rule);
                }
                else
                {
                    throw new Exception($"Invalid rule: {line} in {csvPath}!");
                }
            }

            _mergeRules.Add(ler2Type, rules);
        }
        public HashSet<HashSet<SerializablePolyline3d>> Validate(
            HashSet<HashSet<SerializablePolyline3d>> plines,
            StringBuilder log)
        {
            if (_mergeRules.Count == 0)
                throw new Exception("No merge rules loaded!");

            HashSet<HashSet<SerializablePolyline3d>> result =
                new HashSet<HashSet<SerializablePolyline3d>>();

            foreach (var group in plines)
            {
                bool canMerge = true;
                StringBuilder localLog = new StringBuilder();
                // Compare all objects with the first one in the subcollection
                var robj = group.First();
                string ler2Type = robj.Properties["Ler2Type"].ToString();
                MergeRules mergeRules = _mergeRules[ler2Type];
                SerializablePolyline3d[] pl3ds = group.ToArray();
                localLog.AppendLine($"Group {robj.GroupNumber} cannot be merged! Reasons:");
                for (int i = 1; i < pl3ds.Length; i++)
                {
                    foreach (var rule in mergeRules.PropertyRules)
                    {
                        var propertyName = rule.Key;
                        var mergeRule = rule.Value;

                        if (mergeRule == MergeRuleType.MustMatch)
                        {
                            object referenceValue;
                            object compareValue;

                            robj.Properties.TryGetValue(propertyName, out referenceValue);
                            pl3ds[i].Properties.TryGetValue(propertyName, out compareValue);

                            string referenceString = Convert.ToString(referenceValue);
                            string compareString = Convert.ToString(compareValue);

                            if (!referenceString.Equals(compareString))
                            {
                                canMerge = false;
                                localLog.AppendLine($"Property '{propertyName}' does not match: " +
                                    $"{string.Join(", ", group.Select(x => x.Properties[propertyName].ToString()))}.");
                            }
                        }
                    }
                }

                if (canMerge) result.Add(group);
                else log.AppendLine(localLog.ToString());
            }

            return result;
        }
    }

    public enum MergeRuleType
    {
        MustMatch,
        Ignore
    }

    public class MergeRules
    {
        public Dictionary<string, MergeRuleType> PropertyRules { get; set; }
            = new Dictionary<string, MergeRuleType>();
    }
}
