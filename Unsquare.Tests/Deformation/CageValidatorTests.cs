using NUnit.Framework;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Tests.Deformation
{
    public class CageValidatorTests
    {
        private static readonly CageBox Box = new(new float3(-5, 0, -5), new float3(5, 10, 5));

        [Test]
        public void Identity_IsValid()
        {
            Assert.That(CageValidator.Validate(Box, CageOffsets.Identity, CageLimits.Default), Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void ModerateTaper_IsValid()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Edge(3), new float3(3, 0, 0));
            Assert.That(CageValidator.Validate(Box, offsets, CageLimits.Default), Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void TopFacePushedBelowBottom_IsInverted()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, -12, 0));
            Assert.That(CageValidator.Validate(Box, offsets, CageLimits.Default), Is.EqualTo(CageValidity.Inverted));
        }

        [Test]
        public void CornerPulledThroughOpposite_IsInverted()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(-11, 0, 0));
            Assert.That(CageValidator.Validate(Box, offsets, CageLimits.Default), Is.EqualTo(CageValidity.Inverted));
        }

        [Test]
        public void HugeOffset_IsOutOfRange()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(0), new float3(0, 0, -25));
            Assert.That(CageValidator.Validate(Box, offsets, CageLimits.Default), Is.EqualTo(CageValidity.OutOfRange));
        }

        [Test]
        public void Clamp_BringsOffsetsWithinLimit()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(1), new float3(100, 0, 0));
            var clamped = CageValidator.Clamp(Box, offsets, CageLimits.Default);
            Assert.That(clamped.Meters(1).x, Is.EqualTo(CageValidator.OffsetLimit(Box, CageLimits.Default).x).Within(1e-4f));
            Assert.That(CageValidator.Validate(Box, clamped, CageLimits.Default), Is.EqualTo(CageValidity.Valid));
        }
    
        [Test]
        public void Validate_LargerOffsetFactor_AcceptsFartherMoves()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Corner(7), new float3(25, 0, 0));
            Assert.That(CageValidator.Validate(Box, offsets, CageLimits.Default), Is.EqualTo(CageValidity.OutOfRange));
            var generous = new CageLimits(3f, 5f, 0.05f);
            Assert.That(CageValidator.Validate(Box, offsets, generous), Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void OffsetLimit_ThinBox_UsesMinimumRange()
        {
            var thin = new CageBox(new float3(0, 0, 0), new float3(10, 10, 1));
            var limits = new CageLimits(2f, 8f, 0.05f);
            Assert.That(CageValidator.OffsetLimit(thin, limits), Is.EqualTo(new float3(20, 20, 8)));
        }

        [Test]
        public void OffsetLimit_LongLowBox_VerticalScalesWithLongestSide()
        {
            var lot = new CageBox(new float3(-20, 0, -60), new float3(20, 8, 60));
            Assert.That(CageValidator.OffsetLimit(lot, CageLimits.Default), Is.EqualTo(new float3(80, 240, 240)));
        }

        [Test]
        public void Clamp_BackColumnsFarDownSlope_KeepsDrop()
        {
            var lot = new CageBox(new float3(-20, 0, -60), new float3(20, 8, 60));
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(4), new float3(0, -30, 0));
            Assert.That(CageValidator.Clamp(lot, offsets, CageLimits.Default), Is.EqualTo(offsets));
            Assert.That(CageValidator.Validate(lot, offsets, CageLimits.Default), Is.EqualTo(CageValidity.Valid));
        }

        [Test]
        public void Validate_StricterVolumeRatio_RejectsStrongSqueeze()
        {
            var offsets = CageOffsets.Identity.Move(CageHandle.Face(3), new float3(0, -8, 0));
            Assert.That(CageValidator.Validate(Box, offsets, CageLimits.Default), Is.EqualTo(CageValidity.Valid));
            Assert.That(CageValidator.Validate(Box, offsets, new CageLimits(2f, 5f, 0.5f)), Is.EqualTo(CageValidity.Inverted));
        }

        [TestCase(0f, 5f, 0.05f)]
        [TestCase(2f, -1f, 0.05f)]
        [TestCase(2f, 5f, 0f)]
        [TestCase(2f, 5f, 1f)]
        public void CageLimits_InvalidValues_Throw(float factor, float minRange, float volume)
        {
            Assert.That(() => new CageLimits(factor, minRange, volume), Throws.ArgumentException);
        }
    }
}
