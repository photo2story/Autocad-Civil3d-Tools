﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Autodesk.Aec.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.PipeSchedule;
using static IntersectUtilities.ComponentSchedule;

using AcRx = Autodesk.AutoCAD.Runtime;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;
using PsDataType = Autodesk.Aec.PropertyData.DataType;
using csdot.Attributes.Types;

namespace IntersectUtilities
{
    public partial class Graph
    {
        private static class QA
        {
            internal static void QualityAssurance(Edge edge, Database db, System.Data.DataTable dt)
            {
                List<string> errorMsg = new List<string>();

                QA_System(edge, db, dt, errorMsg);

                QA_Dn(edge, db, dt, errorMsg);

                //If errors detected -- fill out the label and set color red
                if (errorMsg.Count > 0)
                {
                    //Add code here to color the edge red!
                    string label = " [ label=\"";
                    label += string.Join(", ", errorMsg.ToArray());
                    label += "\" color=\"red\" ] ";
                    edge.Label = label;
                }

                return;
            }

            /// <summary>
            /// QAs Twin/Enkelt consistency, in this method System = Twin/Enkelt
            /// Returns the error msg.
            /// </summary>
            private static void QA_System(
                Edge edge, Database db, System.Data.DataTable dt, List<string> errorMsg)
            {
                #region Twin/Enkelt test
                Entity ent1 = edge.Id1.Go<Entity>(db);
                Entity ent2 = edge.Id2.Go<Entity>(db);

                //Twin/Bonded test
                PipeTypeEnum type1 = default;
                PipeTypeEnum type2 = default;
                if (ent1 is Polyline) type1 = PipeSchedule.GetPipeType(ent1);
                else if (ent1 is BlockReference br)
                {
                    type1 = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                        ReadComponentSystem(br, dt, edge.EndType1));
                }
                if (ent2 is Polyline) type2 = PipeSchedule.GetPipeType(ent2);
                else if (ent2 is BlockReference br)
                {
                    type2 = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                        ReadComponentSystem(br, dt, edge.EndType2));
                }

                if (type1 == PipeTypeEnum.Retur || type1 == PipeTypeEnum.Frem)
                    type1 = PipeTypeEnum.Enkelt;
                if (type2 == PipeTypeEnum.Retur || type2 == PipeTypeEnum.Frem)
                    type2 = PipeTypeEnum.Enkelt;

                if (type1 != default && type2 != default && type1 != type2)
                    errorMsg.Add("T/E");
                #endregion
            }

            private static void QA_Dn(
                Edge edge, Database db, System.Data.DataTable dt, List<string> errorMsg)
            {
                #region DN QA
                //DN test
                //We cannot determine which end the size comes from
                //But we don't need it actually
                //We check if any of returned DNs is equal to the test value
                //If none is equal, then it is error
                //UPDATE1:  No, it is significant if we are looking at reducers
                //UPDATE2:  Current idea is to implement a full-fledged
                //          look-back-forward to compare entities before and after
                //          the reducer, it is possible because we are in a graph
                //          situation and we are confident that connection information
                //          is present in the PopertySet for connected entities, so
                //          no geometric search/matching is necessary
                Entity ent1 = edge.Id1.Go<Entity>(db);
                Entity ent2 = edge.Id2.Go<Entity>(db);

                int DN11 = default;
                int DN12 = default;
                if (ent1 is Polyline) DN11 = PipeSchedule.GetPipeDN(ent1);
                else if (ent1 is BlockReference br)
                {
                    DN11 = ReadComponentDN1Int(br, dt);
                    DN12 = ReadComponentDN2Int(br, dt);
                }

                int DN21 = default;
                int DN22 = default;
                if (ent2 is Polyline) DN21 = PipeSchedule.GetPipeDN(ent2);
                else if (ent2 is BlockReference br)
                {
                    DN21 = ReadComponentDN1Int(br, dt);
                    DN22 = ReadComponentDN2Int(br, dt);
                }

                HashSet<int> dnList1 = new HashSet<int>();
                if (DN11 != 0) dnList1.Add(DN11);
                if (DN12 != 0) dnList1.Add(DN12);

                HashSet<int> dnList2 = new HashSet<int>();
                if (DN21 != 0) dnList2.Add(DN21);
                if (DN22 != 0) dnList2.Add(DN22);

                if (dnList1.Count == 0 || dnList2.Count == 0)
                {
                    if (dnList1.Count == 0)
                        throw new System.Exception($"Entity {ent1} has wrong DN(s)!");
                    else if (dnList2.Count == 0)
                        throw new System.Exception($"Entity {ent2} has wrong DN(s)!");
                }

                if (dnList1.Count == 1 && dnList2.Count == 1)
                {
                    int DN1 = dnList1.First();
                    int DN2 = dnList2.First();
                    if (DN1 != DN2) errorMsg.Add("DN");
                }
                else if (dnList1.Count == 2 && dnList2.Count == 2)
                {
                    errorMsg.Add("DN forvirring");
                }
                else if (dnList1.Count == 2)
                {
                    int DN = dnList2.First();
                    if (!dnList1.Contains(DN))
                        errorMsg.Add("DN");
                }
                else if (dnList2.Count == 2)
                {
                    int DN = dnList1.First();
                    if (!dnList2.Contains(DN))
                        errorMsg.Add("DN");
                }
                #endregion
            }
        }

    }
}
