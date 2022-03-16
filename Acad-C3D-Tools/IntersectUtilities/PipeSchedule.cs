﻿using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    public static class PipeSchedule
    {
        private static readonly Dictionary<int, double> kOdsS1Twin = new Dictionary<int, double>
            {
                {20,125.0},
                {25,140.0},
                {32,160.0},
                {40,160.0},
                {50,200.0},
                {65,225.0},
                {80,250.0},
                {100,315.0},
                {125,400.0},
                {150,450.0},
                {200,560.0},
                {250,710.0}
            };
        private static readonly Dictionary<int, double> kOdsS2Twin = new Dictionary<int, double>
            {
                {20,140.0},
                {25,160.0},
                {32,180.0},
                {40,180.0},
                {50,225.0},
                {65,250.0},
                {80,280.0},
                {100,355.0},
                {125,450.0},
                {150,500.0},
                {200,630.0},
                {250,800.0}
            };
        private static readonly Dictionary<int, double> kOdsS3Twin = new Dictionary<int, double>
            {
                {  20, 160.0 },
                {  25, 180.0 },
                {  32, 200.0 },
                {  40, 200.0 },
                {  50, 250.0 },
                {  65, 280.0 },
                {  80, 315.0 },
                { 100, 400.0 },
                { 125, 500.0 },
                { 150, 560.0 },
                { 200, 710.0 },
                { 250, 900.0 }
            };
        private static readonly Dictionary<int, double> kOdsS1Bonded = new Dictionary<int, double>
            {
                {20,90.0},
                {25,90.0},
                {32,110.0},
                {40,110.0},
                {50,125.0},
                {65,140.0},
                {80,160.0},
                {100,200.0},
                {125,225.0},
                {150,250.0},
                {200,315.0},
                {250,400.0},
                {300,450.0},
                {350,500.0},
                {400,560.0},
                {450,630.0},
                {500,710.0},
                {600,800.0}
            };
        private static readonly Dictionary<int, double> kOdsS2Bonded = new Dictionary<int, double>
            {
                {20,110.0},
                {25,110.0},
                {32,125.0},
                {40,125.0},
                {50,140.0},
                {65,160.0},
                {80,180.0},
                {100,225.0},
                {125,250.0},
                {150,280.0},
                {200,355.0},
                {250,450.0},
                {300,500.0},
                {350,560.0},
                {400,630.0},
                {450,710.0},
                {500,800.0},
                {600,900.0}
            };
        private static readonly Dictionary<int, double> kOdsS3Bonded = new Dictionary<int, double>
            {
                { 20, 125.0 },
                { 25, 125.0 },
                { 32, 140.0 },
                { 40, 140.0 },
                { 50, 160.0 },
                { 65, 180.0 },
                { 80, 200.0 },
                { 100, 250.0 },
                { 125, 280.0 },
                { 150, 315.0 },
                { 200, 400.0 },
                { 250, 500.0 },
                { 300, 560.0 },
                { 350, 630.0 },
                { 400, 710.0 },
                { 450, 800.0 },
                { 500, 900.0 },
                { 600, 1000.0 }
            };
        public static int GetPipeDN(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN20":
                case "FJV-FREM-DN20":
                case "FJV-RETUR-DN20":
                    return 20;
                case "FJV-TWIN-DN25":
                case "FJV-FREM-DN25":
                case "FJV-RETUR-DN25":
                    return 25;
                case "FJV-TWIN-DN32":
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                    return 32;
                case "FJV-TWIN-DN40":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                    return 40;
                case "FJV-TWIN-DN50":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                    return 50;
                case "FJV-TWIN-DN65":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                    return 65;
                case "FJV-TWIN-DN80":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                    return 80;
                case "FJV-TWIN-DN100":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                    return 100;
                case "FJV-TWIN-DN125":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                    return 125;
                case "FJV-TWIN-DN150":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                    return 150;
                case "FJV-TWIN-DN200":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                    return 200;
                case "FJV-TWIN-DN250":
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                    return 250;
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                    return 300;
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                    return 350;
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                    return 400;
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                    return 450;
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                    return 500;
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return 600;
                default:
                    DocumentCollection docCol = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
                    Editor editor = docCol.MdiActiveDocument.Editor;
                    editor.WriteMessage("\nFor entity: " + ent.Handle.ToString() + " no pipe dimension could be determined!");
                    return 999;
            }
        }
        /// <returns>"Twin" or "Enkelt", null if fail.</returns>
        public static string GetPipeSystem(Entity ent)
        {
            string layer = ent.Layer;
            switch (layer)
            {
                case "FJV-TWIN-DN20":
                case "FJV-TWIN-DN25":
                case "FJV-TWIN-DN32":
                case "FJV-TWIN-DN40":
                case "FJV-TWIN-DN50":
                case "FJV-TWIN-DN65":
                case "FJV-TWIN-DN80":
                case "FJV-TWIN-DN100":
                case "FJV-TWIN-DN125":
                case "FJV-TWIN-DN150":
                case "FJV-TWIN-DN200":
                case "FJV-TWIN-DN250":
                    return "Twin";
                case "FJV-FREM-DN20":
                case "FJV-RETUR-DN20":
                case "FJV-FREM-DN25":
                case "FJV-RETUR-DN25":
                case "FJV-FREM-DN32":
                case "FJV-RETUR-DN32":
                case "FJV-FREM-DN40":
                case "FJV-RETUR-DN40":
                case "FJV-FREM-DN50":
                case "FJV-RETUR-DN50":
                case "FJV-FREM-DN65":
                case "FJV-RETUR-DN65":
                case "FJV-FREM-DN80":
                case "FJV-RETUR-DN80":
                case "FJV-FREM-DN100":
                case "FJV-RETUR-DN100":
                case "FJV-FREM-DN125":
                case "FJV-RETUR-DN125":
                case "FJV-FREM-DN150":
                case "FJV-RETUR-DN150":
                case "FJV-FREM-DN200":
                case "FJV-RETUR-DN200":
                case "FJV-FREM-DN250":
                case "FJV-RETUR-DN250":
                case "FJV-FREM-DN300":
                case "FJV-RETUR-DN300":
                case "FJV-FREM-DN350":
                case "FJV-RETUR-DN350":
                case "FJV-FREM-DN400":
                case "FJV-RETUR-DN400":
                case "FJV-FREM-DN450":
                case "FJV-RETUR-DN450":
                case "FJV-FREM-DN500":
                case "FJV-RETUR-DN500":
                case "FJV-FREM-DN600":
                case "FJV-RETUR-DN600":
                    return "Enkelt";
                default:
                    DocumentCollection docCol = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
                    Editor editor = docCol.MdiActiveDocument.Editor;
                    editor.WriteMessage("\nFor entity: " + ent.Handle.ToString() + " no system could be determined!");
                    return null;
            }
        }
        public static double GetPipeOd(Entity ent)
        {
            Dictionary<int, double> Ods = new Dictionary<int, double>()
            {
                { 10, 17.2 },
                { 15, 21.3 },
                { 20, 26.9 },
                { 25, 33.7 },
                { 32, 42.4 },
                { 40, 48.3 },
                { 50, 60.3 },
                { 65, 76.1 },
                { 80, 88.9 },
                { 100, 114.3 },
                { 125, 139.7 },
                { 150, 168.3 },
                { 200, 219.1 },
                { 250, 273.0 },
                { 300, 323.9 },
                { 350, 355.6 },
                { 400, 406.4 },
                { 450, 457.0 },
                { 500, 508.0 },
                { 600, 610.0 },
            };

            int dn = GetPipeDN(ent);
            if (Ods.ContainsKey(dn)) return Ods[dn];
            return 0;
        }
        /// <summary>
        /// WARNING! Currently Series 3 only.
        /// </summary>
        public static double GetTwinPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1Twin.ContainsKey(dn)) return kOdsS1Twin[dn];
                    else return 0;
                case PipeSeriesEnum.S2:
                    if (kOdsS2Twin.ContainsKey(dn)) return kOdsS2Twin[dn];
                    else return 0;
                case PipeSeriesEnum.S3:
                    if (kOdsS3Twin.ContainsKey(dn)) return kOdsS3Twin[dn];
                    else return 0;
                default:
                    return 0;
            }
        }
        public static double GetTwinPipeKOd(Entity ent)
        {
            int DN = GetPipeDN(ent);
            if (kOdsS3Twin.ContainsKey(DN)) return kOdsS3Twin[DN];
            return 0;
        }
        public static double GetTwinPipeKOd(int DN)
        {
            if (kOdsS3Twin.ContainsKey(DN)) return kOdsS3Twin[DN];
            return 0;
        }
        /// <summary>
        /// WARNING! Currently S3 only.
        /// </summary>
        public static double GetBondedPipeKOd(Entity ent, PipeSeriesEnum pipeSeries)
        {
            int dn = GetPipeDN(ent);
            switch (pipeSeries)
            {
                case PipeSeriesEnum.S1:
                    if (kOdsS1Bonded.ContainsKey(dn)) return kOdsS1Bonded[dn];
                    break;
                case PipeSeriesEnum.S2:
                    if (kOdsS2Bonded.ContainsKey(dn)) return kOdsS2Bonded[dn];
                    break;
                case PipeSeriesEnum.S3:
                    if (kOdsS3Bonded.ContainsKey(dn)) return kOdsS3Bonded[dn];
                    break;
                default:
                    return 0;
            }
            return 0;
        }
        public static double GetBondedPipeKOd(Entity ent)
        {
            int dn = GetPipeDN(ent);
            if (kOdsS3Bonded.ContainsKey(dn)) return kOdsS3Bonded[dn];
            return 0;
        }
        public static double GetBondedPipeKOd(int DN)
        {
            if (kOdsS3Bonded.ContainsKey(DN)) return kOdsS3Bonded[DN];
            return 0;
        }
        public static double GetPipeKOd(Entity ent, PipeSeriesEnum pipeSeries) =>
            GetPipeSystem(ent) == "Twin" ?
            GetTwinPipeKOd(ent, pipeSeries) : GetBondedPipeKOd(ent, pipeSeries);
        public static double GetPipeKOd(Entity ent) =>
            GetPipeSystem(ent) == "Twin" ? GetTwinPipeKOd(ent) : GetBondedPipeKOd(ent);
        public static string GetPipeSeries(Entity ent) => "S3";
        public static PipeSeriesEnum GetPipeSeriesV2(Entity ent)
        {
            double realKod = ((Polyline)ent).ConstantWidth;
            double kod = GetPipeKOd(ent, PipeSeriesEnum.S3) / 1000;
            if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S3;
            kod = GetPipeKOd(ent, PipeSeriesEnum.S2) / 1000;
            if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S2;
            kod = GetPipeKOd(ent, PipeSeriesEnum.S1) / 1000;
            if (Equalz(kod, realKod, 0.0001)) return PipeSeriesEnum.S1;

            bool Equalz(double x, double y, double eps)
            {
                if (Math.Abs(x - y) < eps) return true;
                else return false;
            }

            throw new System.Exception(
                $"Entity {ent.Handle.ToString()} does not have valid Constant Width!");
        }
        public static string GetPipeSeries(PipeSeriesEnum pipeSeries) => pipeSeries.ToString();
        public static double GetPipeStdLength(Entity ent) => GetPipeDN(ent) <= 80 ? 12 : 16;
        public static bool IsInSituBent(Entity ent)
        {
            string system = GetPipeSystem(ent);
            switch (system)
            {
                case "Twin":
                    if (GetPipeDN(ent) < 65) return true;
                    break;
                case "Enkelt":
                    if (GetPipeDN(ent) < 100) return true;
                    break;
                default:
                    throw new Exception(
                        $"Entity handle {ent.Handle} has invalid layer!");
            }
            return false;
        }
        public static double GetPipeMinElasticRadius(Entity ent, bool considerInSituBending = true)
        {
            if (considerInSituBending && IsInSituBent(ent)) return 0;

            Dictionary<int, double> radii = new Dictionary<int, double>
            {
                { 20, 13.0 },
                { 25, 17.0 },
                { 32, 21.0 },
                { 40, 24.0 },
                { 50, 30.0 },
                { 65, 38.0 },
                { 80, 44.0 },
                { 100, 57.0 },
                { 125, 70.0 },
                { 150, 84.0 },
                { 200, 110.0 },
                { 250, 137.0 },
                { 300, 162.0 },
                { 350, 178.0 },
                { 400, 203.0 },
                { 999, 0.0 }
            };
            return radii[GetPipeDN(ent)];
        }
        internal enum PipeTypeEnum
        {
            Twin,
            Frem,
            Retur
        }
        public enum PipeSeriesEnum
        {
            S1,
            S2,
            S3
        }
        internal enum PipeDnEnum
        {
            DN20,
            DN25,
            DN32,
            DN40,
            DN50,
            DN65,
            DN80,
            DN100,
            DN125,
            DN150,
            DN200,
            DN250,
            DN300,
            DN350,
            DN400,
            DN450,
            DN500,
            DN600
        }
    }
}
