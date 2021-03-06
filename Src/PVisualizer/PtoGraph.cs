﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Pc;
using Microsoft.Pc.Domains;
using Microsoft.Formula.API;
using System.IO;

namespace Microsoft.PVisualizer
{
    class MachineInfo
    {
        public string startStateName;
        public Subgraph subgraph;
        public Dictionary<string, string> stateNameToNodeId;
    }
    class PtoGraph
    {
        private static string MakeNodeId(P_Root.StateDecl stateDecl)
        {
            string stateName = QualifiedNameToString(stateDecl.name as P_Root.QualifiedName);
            string ownerName = ((stateDecl.owner as P_Root.MachineDecl).name as P_Root.StringCnst).Value;
            return string.Format("{0}.{1}", ownerName, stateName);
        }

        private static string QualifiedNameToString(P_Root.QualifiedName qualifiedName)
        {
            string name = (qualifiedName.name as P_Root.StringCnst).Value;
            if (qualifiedName.qualifier is P_Root.UserCnst)
                return name;
            return QualifiedNameToString((P_Root.QualifiedName)qualifiedName.qualifier) + "." + name;
        }

        public static void GenerateDgml(List<PProgram> parsedPrograms, TextWriter writer)
        {
            writer.WriteLine("<DirectedGraph xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\">");
            writer.WriteLine("  <Nodes>");

            HashSet<string> startStates = new HashSet<string>();
            Dictionary<string, string> containsLinks = new Dictionary<string, string>();
            Dictionary<string, MachineInfo> machineNameToMachineInfo = new Dictionary<string, MachineInfo>();

            foreach (var program in parsedPrograms)
            {
                foreach (var machine in program.Machines)
                {
                    var machineName = (machine.name as P_Root.StringCnst).Value;
                    writer.WriteLine("   <Node Id='" + machineName + "' Group='Expanded' Category='Machine'/>");
                    startStates.Add(QualifiedNameToString(machine.start as P_Root.QualifiedName));
                    var machineInfo = new MachineInfo();
                    machineInfo.startStateName = QualifiedNameToString(machine.start as P_Root.QualifiedName);
                    machineInfo.stateNameToNodeId = new Dictionary<string, string>();
                    machineNameToMachineInfo[machineName] = machineInfo;
                }

                foreach (var state in program.States)
                {
                    var nodeId = MakeNodeId(state);
                    var stateName = QualifiedNameToString(state.name as P_Root.QualifiedName);

                    string category = "State";

                    string ownerName = ((state.owner as P_Root.MachineDecl).name as P_Root.StringCnst).Value;
                    var machineInfo = machineNameToMachineInfo[ownerName];

                    if (machineInfo.startStateName == stateName)
                    {
                        category = "EntryPoint";
                    }
                    writer.WriteLine("   <Node Id='" + nodeId + "' Label='" + stateName + "' Category='" + category + "' />");

                    machineInfo.stateNameToNodeId[stateName] = nodeId;

                    containsLinks[ownerName + ":" + nodeId] = nodeId;
                }
            }

            writer.WriteLine("  </Nodes>");
            writer.WriteLine("  <Links>");

            foreach (var link in containsLinks)
            {
                string[] parts = link.Key.Split(':');
                writer.WriteLine("   <Link Source='" + parts[0] + "' Target='" + link.Value + "' Category='Contains' />");
            }

            foreach (var program in parsedPrograms)
            {
                foreach (var machine in program.Machines)
                {
                    foreach (var trans in program.Transitions)
                    {
                        var srcStateDecl = (P_Root.StateDecl)trans.src;
                        var srcMachineDecl = (P_Root.MachineDecl)srcStateDecl.owner;
                        var srcOwnerName = (srcMachineDecl.name as P_Root.StringCnst).Value;

                        var srcStateName = QualifiedNameToString(srcStateDecl.name as P_Root.QualifiedName);
                        var dstStateName = QualifiedNameToString(trans.dst as P_Root.QualifiedName);

                        string trigger;
                        var constant = trans.trig as P_Root.UserCnst;
                        if (constant != null)
                        {
                            var constantValue = (P_Root.UserCnstKind)constant.Value;
                            if (constantValue == P_Root.UserCnstKind.NULL)
                            {
                                trigger = "null";
                            }
                            else
                            {
                                trigger = "halt";
                            }
                        }
                        else
                        {
                            trigger = (trans.trig as P_Root.StringCnst).Value;
                        }

                        writer.WriteLine("   <Link Source='" + srcOwnerName + "." + srcStateName + "' Target='" + srcOwnerName + "." + dstStateName + "' Label='" + trigger + "' />");
                    }
                }
            }

            writer.WriteLine("  </Links>");
            writer.WriteLine("</DirectedGraph>");
        }

        public static Graph GenerateGraph(Dictionary<string, PProgram> parsedPrograms)
        {
            Graph graph = new Graph();
            Dictionary<string, MachineInfo> machineNameToMachineInfo = new Dictionary<string,MachineInfo>();

            foreach (var program in parsedPrograms.Values)
            {
                foreach (var machine in program.Machines)
                {
                    var machineName = (machine.name as P_Root.StringCnst).Value;
                    var machineInfo = new MachineInfo();
                    machineInfo.startStateName = QualifiedNameToString(machine.start as P_Root.QualifiedName);
                    machineInfo.subgraph = new Subgraph(machineName);
                    graph.RootSubgraph.AddSubgraph(machineInfo.subgraph);
                    machineInfo.stateNameToNodeId = new Dictionary<string, string>();
                    machineNameToMachineInfo[machineName] = machineInfo;
                }

                foreach (var state in program.States)
                {
                    var nodeId = MakeNodeId(state);
                    var stateName = QualifiedNameToString(state.name as P_Root.QualifiedName);
                    Node node = new Node(nodeId);
                    node.LabelText = stateName;
                    graph.AddNode(node);
                    string ownerName = ((state.owner as P_Root.MachineDecl).name as P_Root.StringCnst).Value;
                    var machineInfo = machineNameToMachineInfo[ownerName];
                    machineInfo.subgraph.AddNode(node);
                    if (machineInfo.startStateName == stateName)
                    {
                        node.Attr.LineWidth *= 3;
                    }
                    machineInfo.stateNameToNodeId[stateName] = nodeId;

                    var temperature = state.temperature as P_Root.UserCnst;
                    var temperatureValue = (P_Root.UserCnstKind)temperature.Value;
                    if (temperatureValue == P_Root.UserCnstKind.HOT)
                    {
                        node.Attr.Color = Color.Red;
                    }
                    else if (temperatureValue == P_Root.UserCnstKind.COLD)
                    {
                        node.Attr.Color = Color.Blue;
                    }
                }

                foreach (var trans in program.Transitions)
                {
                    var srcStateDecl = (P_Root.StateDecl)trans.src;
                    var ownerMachineDecl = (P_Root.MachineDecl)srcStateDecl.owner;
                    var ownerName = (ownerMachineDecl.name as P_Root.StringCnst).Value;
                    var machineInfo = machineNameToMachineInfo[ownerName];
                    var srcStateName = QualifiedNameToString(srcStateDecl.name as P_Root.QualifiedName);
                    var dstStateName = QualifiedNameToString(trans.dst as P_Root.QualifiedName);
                    string trigger;
                    var constant = trans.trig as P_Root.UserCnst;
                    if (constant != null)
                    {
                        var constantValue = (P_Root.UserCnstKind)constant.Value;
                        if (constantValue == P_Root.UserCnstKind.NULL)
                        {
                            trigger = "null";
                        }
                        else
                        {
                            trigger = "halt";
                        }
                    }
                    else
                    {
                        trigger = (trans.trig as P_Root.StringCnst).Value;
                    }
                    var edge = graph.AddEdge(machineInfo.stateNameToNodeId[srcStateName], trigger, machineInfo.stateNameToNodeId[dstStateName]);
                    if (trans.action is P_Root.UserCnst)
                    {
                        edge.Attr.LineWidth *= 3;
                    }
                }
            }
            return graph;
        }
    }
}
