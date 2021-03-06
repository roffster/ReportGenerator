﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Palmmedia.ReportGenerator.Parser.Analysis;
using Palmmedia.ReportGenerator.Properties;
using Palmmedia.ReportGenerator.Reporting.CodeAnalysis;
using Palmmedia.ReportGenerator.Reporting.Rendering;

namespace Palmmedia.ReportGenerator.Reporting
{
    /// <summary>
    /// Implementation of <see cref="IReportBuilder"/> that uses <see cref="IReportRenderer"/> to create reports.
    /// </summary>
    public abstract class ReportBuilderBase : IReportBuilder
    {
        /// <summary>
        /// Gets the report type.
        /// </summary>
        /// <value>
        /// The report type.
        /// </value>
        public abstract string ReportType { get; }

        /// <summary>
        /// Gets or sets the report configuration.
        /// </summary>
        /// <value>
        /// The report configuration.
        /// </value>
        public IReportConfiguration ReportConfiguration { get; set; }

        /// <summary>
        /// Creates a class report.
        /// </summary>
        /// <param name="reportRenderer">The report renderer.</param>
        /// <param name="class">The class.</param>
        /// <param name="fileAnalyses">The file analyses that correspond to the class.</param>
        public virtual void CreateClassReport(IReportRenderer reportRenderer, Class @class, IEnumerable<FileAnalysis> fileAnalyses)
        {
            if (reportRenderer == null)
            {
                throw new ArgumentNullException(nameof(reportRenderer));
            }

            if (@class == null)
            {
                throw new ArgumentNullException(nameof(@class));
            }

            if (fileAnalyses == null)
            {
                throw new ArgumentNullException(nameof(fileAnalyses));
            }

            reportRenderer.BeginClassReport(this.ReportConfiguration.TargetDirectory, @class.Assembly.ShortName, @class.Name);

            reportRenderer.Header(ReportResources.Summary);

            reportRenderer.BeginKeyValueTable();
            reportRenderer.KeyValueRow(ReportResources.Class, @class.Name);
            reportRenderer.KeyValueRow(ReportResources.Assembly, @class.Assembly.ShortName);
            reportRenderer.KeyValueRow(ReportResources.Files3, @class.Files.Select(f => f.Path));
            reportRenderer.KeyValueRow(ReportResources.CoveredLines, @class.CoveredLines.ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.UncoveredLines, (@class.CoverableLines - @class.CoveredLines).ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.CoverableLines, @class.CoverableLines.ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.TotalLines, @class.TotalLines.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.Coverage2, @class.CoverageQuota.HasValue ? @class.CoverageQuota.Value.ToString(CultureInfo.InvariantCulture) + "%" : string.Empty);

            decimal? branchCoverage = @class.BranchCoverageQuota;

            if (branchCoverage.HasValue)
            {
                reportRenderer.KeyValueRow(ReportResources.BranchCoverage2, branchCoverage.Value.ToString(CultureInfo.InvariantCulture) + "%");
            }

            if (this.ReportConfiguration.Tag != null)
            {
                reportRenderer.KeyValueRow(ReportResources.Tag, this.ReportConfiguration.Tag);
            }

            reportRenderer.FinishTable();

            if (@class.HistoricCoverages.Any(h => h.CoverageQuota.HasValue || h.BranchCoverageQuota.HasValue))
            {
                reportRenderer.Header(ReportResources.History);
                reportRenderer.Chart(@class.HistoricCoverages);
            }

            var metrics = @class.MethodMetrics;

            if (metrics.Any())
            {
                reportRenderer.Header(ReportResources.Metrics);

                reportRenderer.BeginMetricsTable(metrics.First());

                foreach (var metric in metrics)
                {
                    reportRenderer.MetricsRow(metric);
                }

                reportRenderer.FinishTable();
            }

            reportRenderer.Header(ReportResources.Files);

            if (fileAnalyses.Any())
            {
                int fileIndex = 0;
                foreach (var fileAnalysis in fileAnalyses)
                {
                    reportRenderer.File(fileAnalysis.Path);

                    if (!string.IsNullOrEmpty(fileAnalysis.Error))
                    {
                        reportRenderer.Paragraph(fileAnalysis.Error);
                    }
                    else
                    {
                        reportRenderer.BeginLineAnalysisTable(new[] { string.Empty, "#", ReportResources.Line, string.Empty, ReportResources.Coverage });

                        foreach (var line in fileAnalysis.Lines)
                        {
                            reportRenderer.LineAnalysis(fileIndex, line);
                        }

                        reportRenderer.FinishTable();
                    }

                    fileIndex++;
                }
            }
            else
            {
                reportRenderer.Paragraph(ReportResources.NoFilesFound);
            }

            reportRenderer.AddFooter();

            if (fileAnalyses.Any())
            {
                var testMethods = @class.Files
                    .SelectMany(f => f.TestMethods)
                    .Distinct()
                    .OrderBy(l => l.ShortName);

                var codeElementsByFileIndex = new Dictionary<int, IEnumerable<CodeElement>>();

                int fileIndex = 0;
                foreach (var file in @class.Files)
                {
                    codeElementsByFileIndex.Add(fileIndex++, file.CodeElements.OrderBy(c => c.Line));
                }

                reportRenderer.TestMethods(testMethods, fileAnalyses, codeElementsByFileIndex);
            }

            reportRenderer.SaveClassReport(this.ReportConfiguration.TargetDirectory, @class.Assembly.ShortName, @class.Name);
        }

        /// <summary>
        /// Creates the summary report.
        /// </summary>
        /// <param name="reportRenderer">The report renderer.</param>
        /// <param name="summaryResult">The summary result.</param>
        public virtual void CreateSummaryReport(IReportRenderer reportRenderer, SummaryResult summaryResult)
        {
            if (reportRenderer == null)
            {
                throw new ArgumentNullException(nameof(reportRenderer));
            }

            if (summaryResult == null)
            {
                throw new ArgumentNullException(nameof(summaryResult));
            }

            reportRenderer.BeginSummaryReport(this.ReportConfiguration.TargetDirectory, null, ReportResources.Summary);
            reportRenderer.Header(ReportResources.Summary);

            reportRenderer.BeginKeyValueTable();
            reportRenderer.KeyValueRow(ReportResources.GeneratedOn, DateTime.Now.ToShortDateString() + " - " + DateTime.Now.ToLongTimeString());
            reportRenderer.KeyValueRow(ReportResources.Parser, summaryResult.UsedParser);
            reportRenderer.KeyValueRow(ReportResources.Assemblies2, summaryResult.Assemblies.Count().ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.Classes, summaryResult.Assemblies.SelectMany(a => a.Classes).Count().ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.Files2, summaryResult.Assemblies.SelectMany(a => a.Classes).SelectMany(a => a.Files).Distinct().Count().ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.CoveredLines, summaryResult.CoveredLines.ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.UncoveredLines, (summaryResult.CoverableLines - summaryResult.CoveredLines).ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.CoverableLines, summaryResult.CoverableLines.ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.TotalLines, summaryResult.TotalLines.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
            reportRenderer.KeyValueRow(ReportResources.Coverage2, summaryResult.CoverageQuota.HasValue ? summaryResult.CoverageQuota.Value.ToString(CultureInfo.InvariantCulture) + "%" : string.Empty);

            decimal? branchCoverage = summaryResult.BranchCoverageQuota;

            if (branchCoverage.HasValue)
            {
                reportRenderer.KeyValueRow(ReportResources.BranchCoverage2, branchCoverage.Value.ToString(CultureInfo.InvariantCulture) + "%");
            }

            if (this.ReportConfiguration.Tag != null)
            {
                reportRenderer.KeyValueRow(ReportResources.Tag, this.ReportConfiguration.Tag);
            }

            reportRenderer.FinishTable();

            var historicCoverages = this.GetOverallHistoricCoverages(this.ReportConfiguration.OverallHistoricCoverages);
            if (historicCoverages.Any(h => h.CoverageQuota.HasValue || h.BranchCoverageQuota.HasValue))
            {
                reportRenderer.Header(ReportResources.History);
                reportRenderer.Chart(historicCoverages);
            }

            var summableMetrics = summaryResult.Assemblies
                .SelectMany(a => a.Classes)
                .SelectMany(c => c.MethodMetrics)
                .SelectMany(m => m.Metrics)
                .Where(m => m.MetricType == MetricType.CoverageAbsolute)
                .GroupBy(m => m.Name)
                .Select(g => new Metric(g.Key, g.First().ExplanationUrl, MetricType.CoverageAbsolute, g.Sum(m => m.Value)))
                .ToArray();

            if (summableMetrics.Length > 0)
            {
                reportRenderer.Header(ReportResources.Metrics);

                var methodMetric = new MethodMetric(ReportResources.Total, summableMetrics);
                reportRenderer.BeginMetricsTable(methodMetric);
                reportRenderer.MetricsRow(methodMetric);

                reportRenderer.FinishTable();
            }

            var hotspots = RiskHotspotsAnalysis.DetectHotspotsByMetricName(summaryResult.Assemblies);
            if (hotspots.Any())
            {
                reportRenderer.Header(ReportResources.RiskHotspots);
                reportRenderer.RiskHotspots(hotspots);
            }

            reportRenderer.Header(ReportResources.Coverage3);

            if (summaryResult.Assemblies.Any())
            {
                reportRenderer.BeginSummaryTable(summaryResult.SupportsBranchCoverage);

                foreach (var assembly in summaryResult.Assemblies)
                {
                    reportRenderer.SummaryAssembly(assembly, summaryResult.SupportsBranchCoverage);

                    foreach (var @class in assembly.Classes)
                    {
                        reportRenderer.SummaryClass(@class, summaryResult.SupportsBranchCoverage);
                    }
                }

                reportRenderer.FinishTable();
            }
            else
            {
                reportRenderer.Paragraph(ReportResources.NoCoveredAssemblies);
            }

            reportRenderer.CustomSummary(summaryResult.Assemblies, hotspots, summaryResult.SupportsBranchCoverage);

            reportRenderer.AddFooter();
            reportRenderer.SaveSummaryReport(this.ReportConfiguration.TargetDirectory);
        }

        /// <summary>
        /// Creates a class report.
        /// </summary>
        /// <param name="class">The class.</param>
        /// <param name="fileAnalyses">The file analyses that correspond to the class.</param>
        public abstract void CreateClassReport(Class @class, IEnumerable<FileAnalysis> fileAnalyses);

        /// <summary>
        /// Creates the summary report.
        /// </summary>
        /// <param name="summaryResult">The summary result.</param>
        public abstract void CreateSummaryReport(SummaryResult summaryResult);

        /// <summary>
        /// Gets the overall historic coverages from all classes grouped by execution time.
        /// </summary>
        /// <param name="overallHistoricCoverages">All historic coverage elements.</param>
        /// <returns>
        /// The overall historic coverages from all classes grouped by execution time.
        /// </returns>
        protected virtual IEnumerable<HistoricCoverage> GetOverallHistoricCoverages(IEnumerable<HistoricCoverage> overallHistoricCoverages)
        {
            var executionTimes = overallHistoricCoverages
                .Select(h => h.ExecutionTime)
                .Distinct()
                .OrderBy(e => e);

            var result = new List<HistoricCoverage>();

            foreach (var executionTime in executionTimes)
            {
                var historicCoveragesOfExecutionTime = overallHistoricCoverages
                    .Where(h => h.ExecutionTime.Equals(executionTime))
                    .ToArray();

                result.Add(new HistoricCoverage(executionTime, historicCoveragesOfExecutionTime[0].Tag)
                {
                    CoveredLines = historicCoveragesOfExecutionTime.Sum(h => h.CoveredLines),
                    CoverableLines = historicCoveragesOfExecutionTime.Sum(h => h.CoverableLines),
                    CoveredBranches = historicCoveragesOfExecutionTime.Sum(h => h.CoveredBranches),
                    TotalBranches = historicCoveragesOfExecutionTime.Sum(h => h.TotalBranches),
                    TotalLines = historicCoveragesOfExecutionTime.Sum(h => h.TotalLines)
                });
            }

            return result;
        }
    }
}
