using System.Collections.Generic;
using NUnit.Framework;
using Unsquare.Core.Scheduling;

namespace Unsquare.Tests.Scheduling
{
    public class SwapGroupsTests
    {
        private SwapGroups<int> _groups;
        private Dictionary<int, SwapMemberState> _states;

        [SetUp]
        public void SetUp()
        {
            _groups = new SwapGroups<int>();
            _states = new Dictionary<int, SwapMemberState>();
        }

        private SwapMemberState State(int member) =>
            _states.TryGetValue(member, out var state) ? state : SwapMemberState.Done;

        [Test]
        public void IsHeld_NotInGroup_IsFalse()
        {
            Assert.That(_groups.IsHeld(5), Is.False);
        }

        [Test]
        public void Update_OneMemberWaiting_HoldsAll()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _states[1] = SwapMemberState.Ready;
            _states[2] = SwapMemberState.Waiting;
            _groups.Update(State, 0);
            Assert.That(_groups.IsHeld(1), Is.True);
            Assert.That(_groups.IsHeld(2), Is.True);
        }

        [Test]
        public void Update_AllReady_ReleasesAll()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _states[1] = SwapMemberState.Ready;
            _states[2] = SwapMemberState.Ready;
            Assert.That(_groups.Update(State, 0), Is.EqualTo(0));
            Assert.That(_groups.IsHeld(1), Is.False);
            Assert.That(_groups.IsHeld(2), Is.False);
        }

        [Test]
        public void Update_FailedMember_NoLongerWaitedFor()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _states[1] = SwapMemberState.Ready;
            _states[2] = SwapMemberState.Done;
            _groups.Update(State, 0);
            Assert.That(_groups.IsHeld(1), Is.False);
        }

        [Test]
        public void Update_DeadlinePassed_OpensAndCountsIt()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _states[1] = SwapMemberState.Ready;
            _states[2] = SwapMemberState.Waiting;
            Assert.That(_groups.Update(State, 10), Is.EqualTo(1));
            Assert.That(_groups.IsHeld(1), Is.False);
        }

        [Test]
        public void Update_AllDone_RemovesGroup()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _groups.Update(State, 0);
            Assert.That(_groups.Count, Is.EqualTo(0));
        }

        [Test]
        public void Update_OpenGroupMemberWaitsAgain_StaysOpen()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _states[1] = SwapMemberState.Ready;
            _states[2] = SwapMemberState.Ready;
            _groups.Update(State, 0);
            _states[1] = SwapMemberState.Waiting;
            _groups.Update(State, 1);
            Assert.That(_groups.IsHeld(1), Is.False);
        }

        [Test]
        public void Add_MemberOfOlderGroup_MovesToNewGroup()
        {
            _groups.Add(new[] { 1, 2 }, 10);
            _groups.Add(new[] { 2, 3 }, 10);
            _states[1] = SwapMemberState.Ready;
            _states[2] = SwapMemberState.Waiting;
            _states[3] = SwapMemberState.Ready;
            _groups.Update(State, 0);
            Assert.That(_groups.IsHeld(1), Is.False);
            Assert.That(_groups.IsHeld(3), Is.True);
        }
    }
}
