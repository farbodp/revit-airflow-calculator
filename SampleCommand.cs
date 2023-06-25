using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit_Sandbox.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SampleCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDocument = commandData.Application.ActiveUIDocument;
            var document = uiDocument.Document;

            var elementId = uiDocument.Selection.PickObject(ObjectType.Element, "Please select an element to start with");
            if (elementId == null) return Result.Cancelled;

            var selectedElement = document.GetElement(elementId);

            // Get all connected air terminals and calculate total airflow
            var totalAirflow = CalculateTotalAirflow(selectedElement);

            // Convert air flow unit from cubic foot per second to liters per second.
            var totalAirFlowLitersPerSecond = UnitUtils.ConvertFromInternalUnits(totalAirflow, UnitTypeId.LitersPerSecond);
            TaskDialog.Show("Sample Command", $"Total Airflow: {totalAirFlowLitersPerSecond} L/s", TaskDialogCommonButtons.Ok);

            return Result.Succeeded;
        }

        private double CalculateTotalAirflow(Element element)
        {
            var airTerminals = new List<ElementId>();
            var visited = new HashSet<ElementId>();

            TraverseConnections(element, airTerminals, visited);

            var totalAirflow = 0.0;

            foreach (var terminalId in airTerminals)
            {
                var terminal = element.Document.GetElement(terminalId) as FamilyInstance;
                var airflow = terminal.get_Parameter(BuiltInParameter.RBS_DUCT_FLOW_PARAM)?.AsDouble() ?? 0;
                totalAirflow += airflow;
            }

            return totalAirflow;
        }

        // This function uses depth-first search (DSF) algorithm to recursively search the graph nodes and find the air terminal Ids.
        private void TraverseConnections(Element element, List<ElementId> airTerminals, HashSet<ElementId> visited)
        {
            visited.Add(element.Id);

            // Check if the element is an air terminal
            if (IsAirTerminal(element))
            {
                airTerminals.Add(element.Id);
            }

            // Get connected elements
            var connectorSet = GetConnectors(element);

            if (connectorSet != null)
            {
                foreach (var connector in connectorSet)
                {
                    var castConn = connector as Connector;

                    var nextElems = GetNextConnectedElements(castConn, element, visited);

                    foreach (var nextElem in nextElems)
                    {
                        TraverseConnections(nextElem, airTerminals, visited);
                    }

                }
            }
        }

        public List<Element> GetNextConnectedElements(Connector prevConn, Element prevElem, HashSet<ElementId> visitedElems)
        {
            var connectedElems = new List<Element>();

            foreach (var connRef in prevConn.AllRefs)
            {
                var castConnRef = connRef as Connector;
                if (castConnRef.Owner.Id == prevElem.Id || visitedElems.Contains(castConnRef.Owner.Id)) continue;
                connectedElems.Add(castConnRef.Owner);
            }
            return connectedElems;
        }

        public static ConnectorSet GetConnectors(Element element)
        {
            if (element is FamilyInstance fi && fi.MEPModel != null)
            {
                return fi.MEPModel.ConnectorManager.Connectors;
            }
            else if (element is MEPCurve duct)
            {
                return duct.ConnectorManager.Connectors;
            }

            return null;
        }


        private bool IsAirTerminal(Element element)
        {
            return element.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_DuctTerminal;
        }
    }
}