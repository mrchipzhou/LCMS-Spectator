﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using InformedProteomics.Backend.Data.Composition;
using InformedProteomics.Backend.Data.Sequence;
using InformedProteomics.Backend.Data.Spectrometry;
using InformedProteomics.Backend.MassSpecData;
using LcmsSpectatorModels.Readers;
using LcmsSpectatorModels.Utils;
using NUnit.Framework;

namespace LcmsSpectatorTests
{
    [TestFixture]
    public class IonUtilsTest
    {
        /// <summary>
        /// This test checks the observed isotope envelope vs the theoretical to ensure that
        /// they are aligned.
        /// </summary>
        // top down data
        [TestCase(@"\\protoapps\UserData\Wilkins\TopDown\Anil\QC_Shew_IntactProtein_new_CID-30CE-4Sep14_Bane_C2Column_3.raw", 
                  @"\\protoapps\UserData\Wilkins\TopDown\Anil\QC_Shew_IntactProtein_new_CID-30CE-4Sep14_Bane_C2Column_3_IcTda.tsv")]
        // bottom up (dia) data
        [TestCase(@"\\protoapps\UserData\Wilkins\BottomUp\DIA_10mz\data\Q_2014_0523_50_10_fmol_uL_10mz.raw", 
                  @"\\protoapps\UserData\Wilkins\BottomUp\DIA_10mz\data\Q_2014_0523_50_10_fmol_uL_10mz.tsv")]
        // bottom up (dda) data
        [TestCase(@"\\protoapps\UserData\Wilkins\BottomUp\DDA\Q_2014_0523_1_0_amol_uL_DDA.raw",
                  @"\\protoapps\UserData\Wilkins\BottomUp\DDA\Q_2014_0523_1_0_amol_uL_DDA_IcTda.tsv")]
        public void TestIsotopePeakAlignment(string rawFilePath, string idFilePath)
        {
            var idFileReader = IdFileReaderFactory.CreateReader(idFilePath);
            var lcms = PbfLcMsRun.GetLcMsRun(rawFilePath, MassSpecDataType.XCaliburRun);
            var ids = idFileReader.Read();
            ids.SetLcmsRun(lcms, Path.GetFileNameWithoutExtension(rawFilePath));

            var prsms = ids.IdentifiedPrSms;

            const double relIntThres = 0.1;
            var tolerance = new Tolerance(10, ToleranceUnit.Ppm);
            const int maxCharge = 15;
            var ionTypeFactory = new IonTypeFactory(maxCharge);
            var ionTypes = ionTypeFactory.GetAllKnownIonTypes().ToArray();
            foreach (var prsm in prsms)
            {
                foreach (var ionType in ionTypes)
                {
                    var composition = prsm.Sequence.Aggregate(Composition.Zero, (current, aa) => current + aa.Composition);
                    var ion = ionType.GetIon(composition);
                    var observedPeaks = prsm.Ms2Spectrum.GetAllIsotopePeaks(ion, tolerance, relIntThres);
                    if (observedPeaks == null) continue;
                    var errors = IonUtils.GetIsotopePpmError(observedPeaks, ion, relIntThres);
                    foreach (var error in errors)
                    {
                        if (error == null) continue;
                        Assert.True(error <= tolerance.GetValue());
                    }
                }
            }
        }

        [Test]
        public void TestITraqMod()
        {
            var aminoAcidSet = new AminoAcidSet();
            var p = aminoAcidSet.GetAminoAcid('C');
            Console.WriteLine(p.GetMass());

            var itraqMod = Modification.Carbamidomethylation;
            Console.WriteLine(itraqMod.GetMass());

            var modk = new ModifiedAminoAcid(p, itraqMod);
            Console.WriteLine(modk.GetMass());
        }
    }
}
