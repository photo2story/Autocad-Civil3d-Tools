﻿using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using MoreLinq;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.ComponentSchedule;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.PipeSchedule;
using IntersectUtilities.UtilsCommon;
using GroupByCluster;
using QuikGraph;
using QuikGraph.Graphviz;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using System.IO;
using System.Diagnostics;
using QuikGraph.Algorithms.Search;
using QuikGraph.Algorithms;

namespace IntersectUtilities
{
    public class PipelineSizeArray
    {
        public SizeEntry[] SizeArray;
        public BidirectionalGraph<Entity, Edge<Entity>> Graph;
        public int Length { get => SizeArray.Length; }
        public PipelineSizesArrangement Arrangement { get; }
        public int StartingDn { get; }
        public SizeEntry this[int index] { get => SizeArray[index]; }
        public int MaxDn { get => SizeArray.MaxBy(x => x.DN).FirstOrDefault().DN; }
        public int MinDn { get => SizeArray.MinBy(x => x.DN).FirstOrDefault().DN; }
        private System.Data.DataTable dynamicBlocks { get; }
        private HashSet<string> unwantedTypes = new HashSet<string>()
        {
            "Svejsning",
            "Stikafgrening",
            "Muffetee"
        };
        private HashSet<string> graphUnwantedTypes = new HashSet<string>()
        {
            "Svejsning",
        };
        private HashSet<string> directionDefiningTypes = new HashSet<string>()
        {
            "Reduktion"
        };
        /// <summary>
        /// SizeArray listing sizes, station ranges and jacket diameters.
        /// Use empty brs collection or omit it to force size table based on curves.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="brs">All transitions belonging to the current alignment.</param>
        /// <param name="curves">All pipline curves belonging to the current alignment.</param>
        public PipelineSizeArray(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs = default)
        {
            #region Read CSV
            dynamicBlocks = default;
            try
            {
                dynamicBlocks = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
            }
            catch (System.Exception ex)
            {
                prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                prdDbg(ex);
                throw;
            }
            if (dynamicBlocks == default)
            {
                prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                throw new System.Exception("Failed to read FJV Dynamiske Komponenter.csv");
            }
            #endregion

            #region Create graph
            var entities = new HashSet<Entity>(curves);
            if (brs != default) entities.UnionWith(brs);

            HashSet<POI> POIs = new HashSet<POI>();
            foreach (Entity ent in entities) AddEntityToPOIs(ent, POIs);

            IEnumerable<IGrouping<POI, POI>> clusters
                = POIs.GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.01);

            foreach (IGrouping<POI, POI> cluster in clusters)
            {
                //Create unique pairs
                var pairs = cluster.SelectMany((value, index) => cluster.Skip(index + 1),
                                               (first, second) => new { first, second });
                //Create reference to each other for each pair
                foreach (var pair in pairs)
                {
                    if (pair.first.Owner.Handle == pair.second.Owner.Handle) continue;
                    pair.first.AddReference(pair.second);
                    pair.second.AddReference(pair.first);
                }
            }

            //First crate a graph that start from a random entity
            var startingGraph = new BidirectionalGraph<Entity, Edge<Entity>>();
            var groups = POIs.GroupBy(x => x.Owner.Handle);

            foreach (var group in groups)
                startingGraph.AddVertex(group.First().Owner);

            foreach (var group in groups)
            {
                Entity owner = group.First().Owner;

                foreach (var poi in group)
                {
                    foreach (var neighbour in poi.Neighbours)
                    {
                        startingGraph.AddEdge(new Edge<Entity>(owner, neighbour));
                    }
                }
            }

            //Now find the ends, choose one and rearrange graph to start from that node
            var endNodes = startingGraph.Vertices.Where(v => startingGraph.OutDegree(v) == 1 && startingGraph.InDegree(v) == 1);
            var startingNode = endNodes.OrderBy(x => GetStation(al, x)).First();

            var dfs = new DepthFirstSearchAlgorithm<Entity, Edge<Entity>>(startingGraph);
            var verticesInNewOrder = new List<Entity>();
            dfs.FinishVertex += verticesInNewOrder.Add;
            dfs.Compute(startingNode);

            var sortedGraph = new BidirectionalGraph<Entity, Edge<Entity>>();
            sortedGraph.AddVertexRange(verticesInNewOrder);
            foreach (var edge in startingGraph.Edges) sortedGraph.AddEdge(edge);

            Graph = sortedGraph;
            #endregion

            #region Direction
            ////Determine pipe size direction
            #region Old direction method
            ////This is a flawed method using only curves, see below
            //int maxDn = PipeSchedule.GetPipeDN(curves.MaxBy(x => PipeSchedule.GetPipeDN(x)).FirstOrDefault());
            //int minDn = PipeSchedule.GetPipeDN(curves.MinBy(x => PipeSchedule.GetPipeDN(x)).FirstOrDefault());

            //HashSet<(Curve curve, double dist)> curveDistTuples =
            //                new HashSet<(Curve curve, double dist)>();

            //Point3d samplePoint = al.GetPointAtDist(0);

            //foreach (Curve curve in curves)
            //{
            //    if (curve.GetDistanceAtParameter(curve.EndParam) < 0.0001) continue;
            //    Point3d closestPoint = curve.GetClosestPointTo(samplePoint, false);
            //    if (closestPoint != default)
            //        curveDistTuples.Add(
            //            (curve, samplePoint.DistanceHorizontalTo(closestPoint)));
            //}

            //Curve closestCurve = curveDistTuples.MinBy(x => x.dist).FirstOrDefault().curve;

            //StartingDn = PipeSchedule.GetPipeDN(closestCurve); 
            #endregion

            //2023.04.12: A case discovered where there's a reducer after which there's only blocks
            //till the alignment's end. This confuses the code to think that the last size
            //don't exists, as it looks only at polylines present.
            //So, we need to check for presence of reducers to definitely rule out one size case.
            var reducersOrdered = brs?.Where(
                x => x.ReadDynamicCsvProperty(DynamicProperty.Type, dynamicBlocks, false) == "Reduktion")
                .OrderBy(x => al.StationAtPoint(x))
                .ToArray();

            List<int> dnsAlongAlignment = default;
            if (reducersOrdered != null && reducersOrdered.Count() != 0)
            {
                dnsAlongAlignment = new List<int>();

                for (int i = 0; i < reducersOrdered.Count(); i++)
                {
                    var reducer = reducersOrdered[i];

                    if (i == 0) dnsAlongAlignment.Add(
                        GetDirectionallyCorrectReducerDnWithGraph(al, reducer, Side.Left, dynamicBlocks));

                    dnsAlongAlignment.Add(
                        GetDirectionallyCorrectReducerDnWithGraph(al, reducer, Side.Right, dynamicBlocks));
                }
                Arrangement = DetectArrangement(dnsAlongAlignment);
                prdDbg(string.Join(", ", dnsAlongAlignment) + " -> " + Arrangement);
            }
            else
            {
                Arrangement = PipelineSizesArrangement.OneSize;
                prdDbg(Arrangement);
            }

            if (Arrangement == PipelineSizesArrangement.Unknown)
                throw new System.Exception($"Alignment {al.Name} could not determine pipeline sizes direction!");
            #endregion

            //Dispatcher constructor
            if (brs == default || brs.Count == 0 || Arrangement == PipelineSizesArrangement.OneSize)
                SizeArray = ConstructWithCurves(al, curves);
            //else SizeArray = ConstructWithBlocks(al, curves, brs, dynamicBlocks);
            else
            {
                brs = brs.Where(x =>
                    IsTransition(x, dynamicBlocks) ||
                    IsXModel(x, dynamicBlocks)).ToHashSet();

                BlockReference[] brsArray = 
                    brs.OrderBy(x => al.StationAtPoint(x)).ToArray();

                List<SizeEntry> sizes = new List<SizeEntry>();

                int dn = 0;
                double start = 0.0;
                double end = 0.0;
                double kod = 0.0;
                PipeSystemEnum ps = default;
                PipeTypeEnum pt = default;
                PipeSeriesEnum series = default;

                for (int i = 0; i < brsArray.Count(); i++)
                {
                    var br = brsArray[i];

                    //First iteration case
                    if (i == 0)
                    {
                        start = 0.0; end = al.StationAtPoint(br);

                        if (IsTransition(br, dynamicBlocks))
                        {
                            dn = GetDirectionallyCorrectReducerDnWithGraph(al, br, Side.Left);
                            ps = PipeSystemEnum.Stål;
                            pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum), 
                                br.ReadDynamicCsvProperty(DynamicProperty.System, dynamicBlocks), true);
                            series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                                br.ReadDynamicCsvProperty(DynamicProperty.Serie, dynamicBlocks), true);
                            kod = PipeSchedule.GetKOd(dn, pt, ps, series);
                        }
                        
                    }

                    dnsAlongAlignment.Add(
                        GetDirectionallyCorrectReducerDnWithGraph(al, reducer, Side.Right, dynamicBlocks));
                }
            }
        }
        private PipelineSizesArrangement DetectArrangement(List<int> list)
        {
            if (list.Count < 2) return PipelineSizesArrangement.Unknown;

            bool ascendingFlag = false;
            bool descendingFlag = false;
            bool climaxFlag = false;

            for (int i = 1; i < list.Count; ++i)
            {
                if (list[i] > list[i - 1])
                {
                    if (climaxFlag) return PipelineSizesArrangement.MiddleDescendingToEnds;
                    ascendingFlag = true;
                }
                else if (list[i] < list[i - 1])
                {
                    if (ascendingFlag) climaxFlag = true;
                    descendingFlag = true;
                }
            }

            if (ascendingFlag && !descendingFlag) return PipelineSizesArrangement.SmallToLargeAscending;
            if (!ascendingFlag && descendingFlag) return PipelineSizesArrangement.LargeToSmallDescending;
            if (ascendingFlag && descendingFlag) return PipelineSizesArrangement.MiddleDescendingToEnds;

            return PipelineSizesArrangement.Unknown;
        }
        private void AddEntityToPOIs(Entity ent, HashSet<POI> POIs)
        {
            switch (ent)
            {
                case Polyline pline:
                    switch (GetPipeSystem(pline))
                    {
                        case PipeSystemEnum.Ukendt:
                            prdDbg($"Wrong type of pline supplied: {pline.Handle}");
                            return;
                        case PipeSystemEnum.Stål:
                        case PipeSystemEnum.Kobberflex:
                        case PipeSystemEnum.AluPex:
                            POIs.Add(new POI(pline, pline.StartPoint.To2D(), EndType.Start));
                            POIs.Add(new POI(pline, pline.EndPoint.To2D(), EndType.End));
                            break;
                        default:
                            throw new System.Exception("Supplied a new PipeSystemEnum! Add to code kthxbai.");
                    }
                    break;
                case BlockReference br:
                    Transaction tx = br.Database.TransactionManager.TopTransaction;
                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

                    foreach (Oid oid in btr)
                    {
                        if (!oid.IsDerivedFrom<BlockReference>()) continue;
                        BlockReference nestedBr = oid.Go<BlockReference>(tx);
                        if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                        Point3d wPt = nestedBr.Position;
                        wPt = wPt.TransformBy(br.BlockTransform);
                        EndType endType;
                        if (nestedBr.Name.Contains("BRANCH")) { endType = EndType.Branch; }
                        else
                        {
                            endType = EndType.Main;
                        }
                        POIs.Add(new POI(br, wPt.To2D(), endType));
                    }
                    break;
                default:
                    throw new System.Exception("Wrong type of object supplied!");
            }
        }
        public PipelineSizeArray(SizeEntry[] sizeArray) { SizeArray = sizeArray; }
        public PipelineSizeArray GetPartialSizeArrayForPV(ProfileView pv)
        {
            var list = this.GetIndexesOfSizesAppearingInProfileView(pv.StationStart, pv.StationEnd);
            SizeEntry[] partialAr = new SizeEntry[list.Count];
            for (int i = 0; i < list.Count; i++) partialAr[i] = this[list[i]];
            return new PipelineSizeArray(partialAr);
        }
        public SizeEntry GetSizeAtStation(double station)
        {
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                //(stations are END stations!)
                if (station <= curEntry.EndStation) return curEntry;
            }
            return default;
        }
        private int GetDn(Entity entity, System.Data.DataTable dynBlocks)
        {
            if (entity is Polyline pline)
                return PipeSchedule.GetPipeDN(pline);
            else if (entity is BlockReference br)
            {
                if (br.ReadDynamicCsvProperty(DynamicProperty.Type, dynBlocks, false) == "Afgreningsstuds")
                    return ReadComponentDN2Int(br, dynBlocks);
                else return ReadComponentDN1Int(br, dynBlocks);
            }

            else throw new System.Exception("Invalid entity type");
        }
        private double GetStation(Alignment alignment, Entity entity)
        {
            double station = 0;
            double offset = 0;

            switch (entity)
            {
                case Polyline pline:
                    double l = pline.Length;
                    Point3d p = pline.GetPointAtDist(l / 2);
                    alignment.StationOffset(p.X, p.Y, 5.0, ref station, ref offset);
                    break;
                case BlockReference block:
                    try
                    {
                        alignment.StationOffset(block.Position.X, block.Position.Y, 5.0, ref station, ref offset);
                    }
                    catch (Autodesk.Civil.PointNotOnEntityException ex)
                    {
                        prdDbg($"Entity {block.Handle} throws {ex.Message}!");
                        throw;
                    }
                    break;
                default:
                    throw new Exception("Invalid entity type");
            }
            return station;
        }
        public override string ToString()
        {
            //string output = "";
            //for (int i = 0; i < SizeArray.Length; i++)
            //{
            //    output +=
            //        $"{SizeArray[i].DN.ToString("D3")} || " +
            //        $"{SizeArray[i].StartStation.ToString("0000.00")} - {SizeArray[i].EndStation.ToString("0000.00")} || " +
            //        $"{SizeArray[i].Kod.ToString("0")}" +
            //        $"\n";
            //}

            // Convert the struct data to string[][] for easier processing
            string[][] stringData = new string[SizeArray.Length][];
            for (int i = 0; i < SizeArray.Length; i++)
            {
                stringData[i] = SizeArray[i].ToArray();
            }

            // Find the maximum width for each column
            int[] maxColumnWidths = new int[stringData[0].Length];
            for (int col = 0; col < stringData[0].Length; col++)
            {
                maxColumnWidths[col] = stringData.Max(row => row[col].Length);
            }

            // Convert the array to a table string
            string table = "";
            for (int row = 0; row < stringData.Length; row++)
            {
                string line = "";
                for (int col = 0; col < stringData[0].Length; col++)
                {
                    // Right-align each value and add || separator
                    line += stringData[row][col].PadLeft(maxColumnWidths[col]);
                    if (col < stringData[0].Length - 1)
                    {
                        line += " || ";
                    }
                }
                table += line + Environment.NewLine;
            }

            return table;
        }
        private List<int> GetIndexesOfSizesAppearingInProfileView(double pvStationStart, double pvStationEnd)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                if (pvStationStart < curEntry.EndStation &&
                    curEntry.StartStation < pvStationEnd) indexes.Add(i);
            }
            return indexes;
        }
        private SizeEntry[] ConstructWithCurves(Alignment al, HashSet<Curve> curves)
        {
            List<SizeEntry> sizes = new List<SizeEntry>();
            double stepLength = 0.1;
            double alLength = al.Length;
            int nrOfSteps = (int)(alLength / stepLength);
            int previousDn = 0;
            int currentDn = 0;
            double previousKod = 0;
            double currentKod = 0;
            for (int i = 0; i < nrOfSteps + 1; i++)
            {
                double curStationBA = stepLength * i;
                Point3d curSamplePoint = default;
                try { curSamplePoint = al.GetPointAtDist(curStationBA); }
                catch (System.Exception) { continue; }

                HashSet<(Curve curve, double dist, double kappeOd)> curveDistTuples =
                    new HashSet<(Curve curve, double dist, double kappeOd)>();

                foreach (Curve curve in curves)
                {
                    //if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                    Point3d closestPoint = curve.GetClosestPointTo(curSamplePoint, false);
                    if (closestPoint != default)
                        curveDistTuples.Add(
                            (curve, curSamplePoint.DistanceHorizontalTo(closestPoint),
                                PipeSchedule.GetPipeKOd(curve)));
                }
                var result = curveDistTuples.MinBy(x => x.dist).FirstOrDefault();
                //Detect current dn and kod
                currentDn = PipeSchedule.GetPipeDN(result.curve);
                currentKod = result.kappeOd;
                if (currentDn != previousDn || !currentKod.Equalz(previousKod, 1e-6))
                {
                    //Set the previous segment end station unless there's 0 segments
                    if (sizes.Count != 0)
                    {
                        SizeEntry toUpdate = sizes[sizes.Count - 1];
                        sizes[sizes.Count - 1] = new SizeEntry(
                            toUpdate.DN, toUpdate.StartStation, curStationBA, toUpdate.Kod,
                            PipeSchedule.GetPipeSystem(result.curve),
                            PipeSchedule.GetPipeType(result.curve, true),
                            PipeSchedule.GetPipeSeriesV2(result.curve, true));
                    }
                    //Add the new segment; remember, 0 is because the station will be set next iteration
                    //see previous line
                    if (i == 0) sizes.Add(new SizeEntry(currentDn, 0, 0, result.kappeOd,
                        PipeSchedule.GetPipeSystem(result.curve),
                        PipeSchedule.GetPipeType(result.curve, true),
                        PipeSchedule.GetPipeSeriesV2(result.curve, true)));
                    else sizes.Add(new SizeEntry(currentDn, sizes[sizes.Count - 1].EndStation, 0, result.kappeOd,
                        PipeSchedule.GetPipeSystem(result.curve),
                        PipeSchedule.GetPipeType(result.curve, true),
                        PipeSchedule.GetPipeSeriesV2(result.curve, true)));
                }
                //Hand over DN to cache in "previous" variable
                previousDn = currentDn;
                previousKod = currentKod;
                if (i == nrOfSteps)
                {
                    SizeEntry toUpdate = sizes[sizes.Count - 1];
                    sizes[sizes.Count - 1] = new SizeEntry(toUpdate.DN, toUpdate.StartStation, al.Length, toUpdate.Kod,
                        PipeSchedule.GetPipeSystem(result.curve),
                        PipeSchedule.GetPipeType(result.curve, true),
                        PipeSchedule.GetPipeSeriesV2(result.curve, true));
                }
            }

            return sizes.ToArray();
        }
        /// <summary>
        /// Construct SizeArray based on blocks.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="curves">Curves are only here to provide sizes to F- and Y-Models.</param>
        /// <param name="brs">Size changing blocks and transitions (F- and Y-Models).</param>
        /// <param name="dt">Dynamic block datatable.</param>
        /// <returns>SizeArray with sizes for current alignment.</returns>
        private SizeEntry[] ConstructWithBlocks(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs, System.Data.DataTable dt)
        {
            BlockReference[] brsArray = default;

            //New ordering based on station on alignment
            //prdDbg("Using new SizeArray ordering method! Beware!");
            brsArray = brs.OrderBy(x => al.StationAtPoint(x)).ToArray();

            List<SizeEntry> sizes = new List<SizeEntry>();
            double alLength = al.Length;

            int dn = 0;
            double start = 0.0;
            double end = 0.0;
            double kod = 0.0;
            PipeSystemEnum ps = default;
            PipeTypeEnum pt = default;
            PipeSeriesEnum series = default;

            for (int i = 0; i < brsArray.Length; i++)
            {
                BlockReference curBr = brsArray[i];

                if (i == 0)
                {
                    start = 0.0;
                    end = al.StationAtPoint(curBr);

                    //First iteration case
                    if (PipelineSizeArray.IsTransition(curBr, dt))
                    {
                        dn = GetDirectionallyCorrectDn(curBr, Side.Left, dt);
                        //Point3d p3d = al.GetClosestPointTo(curBr.Position, false);
                        //al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                        kod = GetDirectionallyCorrectKod(curBr, Side.Left, dt);
                        ps = PipeSystemEnum.Stål;
                        pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.System, dt), true);
                        series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.Serie, dt), true);
                    }
                    else
                    {//F-Model og Y-Model
                        var ends = curBr.GetAllEndPoints();
                        //Determine connected curves
                        var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                        //Find the curves earlier up the alignment
                        var minCurve = query.MinBy(
                            x => al.StationAtPoint(
                                x.GetPointAtDist(
                                    x.GetDistAtPoint(x.EndPoint) / 2.0)))
                            .FirstOrDefault();

                        if (minCurve == default)
                            throw new Exception($"Br {curBr.Handle} does not find minCurve!");

                        dn = PipeSchedule.GetPipeDN(minCurve);
                        kod = PipeSchedule.GetPipeKOd(minCurve);
                        ps = PipeSchedule.GetPipeSystem(minCurve);
                        pt = PipeSchedule.GetPipeType(minCurve, true);
                        series = PipeSchedule.GetPipeSeriesV2(minCurve, true);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));

                    //Only one member array case
                    //This is an edge case of first iteration
                    if (brsArray.Length == 1)
                    {
                        start = end;
                        end = alLength;

                        if (PipelineSizeArray.IsTransition(curBr, dt))
                        {
                            dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                            kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                            ps = PipeSystemEnum.Stål;
                            pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.System, dt), true);
                            series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.Serie, dt), true);
                        }
                        else
                        {//F-Model og Y-Model
                            var ends = curBr.GetAllEndPoints();
                            //Determine connected curves
                            var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                            //Find the curves further down the alignment
                            var maxCurve = query.MaxBy(
                                x => al.StationAtPoint(
                                    x.GetPointAtDist(
                                        x.GetDistAtPoint(x.EndPoint) / 2.0)))
                                .FirstOrDefault();

                            dn = PipeSchedule.GetPipeDN(maxCurve);
                            kod = PipeSchedule.GetPipeKOd(maxCurve);
                            ps = PipeSchedule.GetPipeSystem(maxCurve);
                            pt = PipeSchedule.GetPipeType(maxCurve, true);
                            series = PipeSchedule.GetPipeSeriesV2(maxCurve, true);
                        }

                        sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                        //This guards against executing further code
                        continue;
                    }
                }

                //General case
                if (i != brsArray.Length - 1)
                {
                    BlockReference nextBr = brsArray[i + 1];
                    start = end;
                    end = al.StationAtPoint(nextBr);

                    if (PipelineSizeArray.IsTransition(curBr, dt))
                    {
                        dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                        //Point3d p3d = al.GetClosestPointTo(nextBr.Position, false);
                        //al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                        kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                        ps = PipeSystemEnum.Stål;
                        pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.System, dt), true);
                        series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.Serie, dt), true);
                    }
                    else
                    {
                        var ends = curBr.GetAllEndPoints();
                        //Determine connected curves
                        var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                        //Find the curves further down the alignment
                        var maxCurve = query.MaxBy(
                            x => al.StationAtPoint(
                                x.GetPointAtDist(
                                    x.GetDistAtPoint(x.EndPoint) / 2.0)))
                            .FirstOrDefault();

                        dn = PipeSchedule.GetPipeDN(maxCurve);
                        kod = PipeSchedule.GetPipeKOd(maxCurve);
                        ps = PipeSchedule.GetPipeSystem(maxCurve);
                        pt = PipeSchedule.GetPipeType(maxCurve, true);
                        series = PipeSchedule.GetPipeSeriesV2(maxCurve, true);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                    //This guards against executing further code
                    continue;
                }

                //And here ends the last iteration
                start = end;
                end = alLength;

                if (PipelineSizeArray.IsTransition(curBr, dt))
                {
                    dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                    kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                    ps = PipeSystemEnum.Stål;
                    pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                        curBr.ReadDynamicCsvProperty(DynamicProperty.System, dt), true);
                    series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                        curBr.ReadDynamicCsvProperty(DynamicProperty.Serie, dt), true);
                }
                else
                {
                    var ends = curBr.GetAllEndPoints();
                    //Determine connected curves
                    var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                    //Find the curves further down the alignment
                    var maxCurve = query.MaxBy(
                        x => al.StationAtPoint(
                            x.GetPointAtDist(
                                x.GetDistAtPoint(x.EndPoint) / 2.0)))
                        .FirstOrDefault();

                    if (maxCurve == default)
                        prdDbg($"Br {curBr.Handle} does not find maxCurve!");

                    dn = PipeSchedule.GetPipeDN(maxCurve);
                    kod = PipeSchedule.GetPipeKOd(maxCurve);
                    ps = PipeSchedule.GetPipeSystem(maxCurve);
                    pt = PipeSchedule.GetPipeType(maxCurve, true);
                    series = PipeSchedule.GetPipeSeriesV2(maxCurve, true);
                }

                sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
            }

            return sizes.ToArray();
        }
        /// <summary>
        /// This method should only be used with a graph that is sorted from the start of the alignment.
        /// Also assuming working only with Reduktion
        /// </summary>
        private int GetDirectionallyCorrectReducerDnWithGraph(Alignment al, BlockReference br, Side side)
        {
            if (Graph == null) throw new Exception("Graph is not initialized!");
            if (br.ReadDynamicCsvProperty(DynamicProperty.Type, dynamicBlocks, false) != "Reduktion")
                throw new Exception($"Method GetDirectionallyCorrectReducerDnWithGraph can only be used with \"Reduktion\"!");

            //Left side means towards the start
            //Right side means towards the end
            double brStation = GetStation(al, br);
            var reducerSizes = new List<int>()
            {
                int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN1,dynamicBlocks)),
                int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN2,dynamicBlocks)),
            };

            //Gather up- and downstream vertici
            var upstreamVertices = new List<Entity>();
            var downstreamVertices = new List<Entity>();

            var dfs = new DepthFirstSearchAlgorithm<Entity, Edge<Entity>>(Graph);
            dfs.TreeEdge += edge =>
            {
                if (GetStation(al, edge.Target) > brStation) downstreamVertices.Add(edge.Target);
                else upstreamVertices.Add(edge.Target);
            };
            dfs.Compute(br);

            bool specialCaseSideSearchFailed = false;
            switch (side)
            {
                case Side.Left:
                    {
                        if (upstreamVertices.Count != 0)
                        {
                            for (int i = 0; i < upstreamVertices.Count; i++)
                            {
                                Entity cur = upstreamVertices[i];

                                int candidate = GetDn(cur, dynamicBlocks);
                                if (candidate == 0) continue;
                                else if (reducerSizes.Contains(candidate)) return candidate;
                            }
                            //If this is reached it means somehow all elements failed to deliver a DN
                            specialCaseSideSearchFailed = true;
                        }
                        else specialCaseSideSearchFailed = true;
                    }
                    break;
                case Side.Right:
                    {
                        if (downstreamVertices.Count != 0)
                        {
                            for (int i = 0; i < downstreamVertices.Count; i++)
                            {
                                Entity cur = downstreamVertices[i];

                                int candidate = GetDn(cur, dynamicBlocks);
                                if (candidate == 0) continue;
                                else if (reducerSizes.Contains(candidate)) return candidate;
                            }
                            //If this is reached it means somehow all elements failed to deliver a DN
                            specialCaseSideSearchFailed = true;
                        }
                        else specialCaseSideSearchFailed = true;
                    }
                    break;
            }

            #region Special case where the reducer is first or last element
            if (specialCaseSideSearchFailed)
            {
                //Use the other side to determine the asked for size
                List<Entity> list;
                if (side == Side.Left) list = downstreamVertices;
                else list = upstreamVertices;

                if (list.Count != 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Entity cur = list[i];

                        int candidate = GetDn(cur, dynamicBlocks);
                        if (candidate == 0) continue;
                        else if (reducerSizes.Contains(candidate))
                            return reducerSizes[0] == candidate ? reducerSizes[1] : reducerSizes[0];
                    }
                    //If this is reached it means somehow BOTH sides failed to deliver a DN
                    specialCaseSideSearchFailed = true;
                }
                else specialCaseSideSearchFailed = true;
            }

            //If this is reached, something is completely wrong
            throw new Exception($"Finding directionally correct sizes for reducer {br.Handle} failed!");
            #endregion
        }
        private int GetDirectionallyCorrectDn(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Arrangement)
            {
                case PipelineSizesArrangement.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN1Int(br, dt);
                    }
                    break;
                case PipelineSizesArrangement.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN2Int(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private double GetDirectionallyCorrectKod(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Arrangement)
            {
                case PipelineSizesArrangement.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN1KodDouble(br, dt);
                    }
                    break;
                case PipelineSizesArrangement.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN2KodDouble(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private static bool IsTransition(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            return type == "Reduktion";
        }
        ///Determines whether the block is an F- or Y-Model
        private static bool IsXModel(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            HashSet<string> transitionTypes = new HashSet<string>()
            {
                "Y-Model",
                "F-Model"
            };

            return transitionTypes.Contains(type);
        }
        internal void Reverse()
        {
            Array.Reverse(this.SizeArray);
        }
        /// <summary>
        /// Unknown - Should throw an exception
        /// OneSize - Cannot be constructed with blocks
        /// SmallToLargeAscending - Small sizes first, blocks preferred
        /// LargeToSmallAscending - Large sizes first, blocks preferred
        /// </summary>
        public enum PipelineSizesArrangement
        {
            Unknown, //Should throw an exception
            OneSize, //Cannot be constructed with blocks
            SmallToLargeAscending, //Blocks preferred
            LargeToSmallDescending, //Blocks preferred
            MiddleDescendingToEnds //When a pipe is supplied from the middle
        }
        private enum Side
        {
            //Left means towards the start of alignment
            Left,
            //Right means towards the end of alignment
            Right
        }
        private struct POI
        {
            public Entity Owner { get; }
            public List<Entity> Neighbours { get; }
            public Point2d Point { get; }
            public EndType EndType { get; }
            public POI(Entity owner, Point2d point, EndType endType)
            { Owner = owner; Point = point; EndType = endType; Neighbours = new List<Entity>(); }
            public bool IsSameOwner(POI toCompare) => Owner.Id == toCompare.Owner.Id;
            internal void AddReference(POI connectedEntity) => Neighbours.Add(connectedEntity.Owner);
        }
    }
    public struct SizeEntry
    {
        public readonly int DN;
        public readonly double StartStation;
        public readonly double EndStation;
        public readonly double Kod;
        public readonly PipeSystemEnum System;
        public readonly PipeTypeEnum Type;
        public readonly PipeSeriesEnum Series;

        public SizeEntry(
            int dn, double startStation, double endStation, double kod, PipeSystemEnum ps, PipeTypeEnum pt, PipeSeriesEnum series)
        {
            DN = dn; StartStation = startStation; EndStation = endStation; Kod = kod; System = ps; Type = pt; Series = series;
        }

        // Get all properties as strings for table conversion
        public string[] ToArray()
        {
            return new string[]
            {
                DN.ToString(),
                StartStation.ToString("F2"),
                EndStation.ToString("F2"),
                Kod.ToString("F0"),
                System.ToString(),
                Type.ToString(),
                Series.ToString()
            };
        }
    }
    public struct PipelineElement
    {
        public readonly int DN1;
        public readonly int DN2;
        public readonly PipelineElementType Type;
        public readonly double Station;
        public PipelineElement(Entity entity, Alignment al, System.Data.DataTable dt)
        {
            switch (entity)
            {
                case Polyline pline:
                    DN1 = PipeSchedule.GetPipeDN(pline);
                    DN2 = 0;
                    Type = PipelineElementType.Pipe;
                    break;
                case BlockReference br:
                    DN1 = int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN1, dt));
                    DN2 = int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN2, dt));
                    if (PipelineElementTypeDict.ContainsKey(br.ReadDynamicCsvProperty(DynamicProperty.Type, dt, false)))
                        Type = PipelineElementTypeDict[br.ReadDynamicCsvProperty(DynamicProperty.Type, dt, false)];
                    else throw new Exception($"Unknown Type: {br.ReadDynamicCsvProperty(DynamicProperty.Type, dt, false)}");
                    break;
                default:
                    throw new System.Exception(
                        $"Entity {entity.Handle} is not a valid pipeline element!");
            }

            Station = GetStation(al, entity);
        }
    }
}
