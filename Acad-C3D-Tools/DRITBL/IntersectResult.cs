﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace DRITBL
{
    internal abstract class IntersectResult
    {
        public IntersectType IntersectType { get; set; }
        public string Vejnavn { get; set; }
        public string Vejklasse { get; set; }
        public string Belægning { get; set; }
        public string Navn { get; set; }
        public string DN1 { get; set; }
        public string DN2 { get; set; }
        public string System { get; set; }
        public string Serie { get; set; }
        public virtual string ToString(ExportType exportType)
        {
            switch (exportType)
            {
                case ExportType.Unknown:
                    break;
                case ExportType.CWO:
                    return $"{Vejnavn};Vejkl. {Vejklasse};{Belægning};{Navn};;;{DN1};{DN2};{System};{Serie};";
                case ExportType.JJR:
                    return $"Vejkl. {Vejklasse};{Belægning};{Navn};{DN1};{DN2};{System};{Serie};";
                default:
                    break;
            }
            return default;
        }
    }
    internal class IntersectResultPipe : IntersectResult
    {
        public IntersectResultPipe()
        {
            IntersectType = IntersectType.Pipe;
            Navn = "Rør præsioleret";
        }
        public double Length { get; set; }
        public override string ToString(ExportType exportType) => 
            base.ToString(exportType) + $"{Length.ToString(new CultureInfo("da-DK"))}";
    }
    internal class IntersectResultComponent : IntersectResult
    {
        public IntersectResultComponent()
        {
            IntersectType = IntersectType.Component;
        }
        public int Count { get; set; }
        public override string ToString(ExportType exportType) => base.ToString(exportType) + $"{Count};";
    }
    internal abstract class PropertyConfig
    {
        public bool Vejnavn { get; set; }
        public bool Vejklasse { get; set; }
        public bool Belægning { get; set; }
        public bool Navn { get; set; }
        public bool DN1 { get; set; }
        public bool DN2 { get; set; }
        public bool System { get; set; }
        public bool Serie { get; set; }
    }
    internal class PropertyConfigCWO : PropertyConfig
    {
        public PropertyConfigCWO()
        {
            Vejnavn = true;
            Vejklasse = true;
            Belægning = true;
            Navn = true;
            DN1 = true;
            DN2 = true;
            System = true;
            Serie = true;
        }
    }
    internal class PropertyConfigJJR : PropertyConfig
    {
        public PropertyConfigJJR()
        {
            Vejnavn = false;
            Vejklasse = true;
            Belægning = true;
            Navn = true;
            DN1 = true;
            DN2 = true;
            System = true;
            Serie = true;
        }
    }
    public enum IntersectType
    {
        Unknown,
        Pipe,
        Component
    }
    public enum ExportType
    {
        Unknown,
        CWO,
        JJR
    }
}
