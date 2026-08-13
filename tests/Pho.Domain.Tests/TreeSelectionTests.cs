using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Pho.Domain.Tests;

public class TreeSelectionTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static IReadOnlyList<TreeRow> Rows(params Guid[] ids)
        => ids.Select(id => new TreeRow
        {
            Kind = TreeRowKind.Group,
            Id = id,
            Depth = 0,
            Group = new Group { Id = id, Name = id.ToString() }
        }).ToList();

    [Fact]
    public void Empty_selection_holds_nothing()
    {
        TreeSelection.Empty.IsEmpty.Should().BeTrue();
        TreeSelection.Empty.Contains(A).Should().BeFalse();
        TreeSelection.Empty.SingleId.Should().BeNull();
    }

    [Fact]
    public void Plain_click_selects_only_that_row_and_becomes_the_anchor()
    {
        var selection = TreeSelection.Empty.SelectOnly(A).SelectOnly(B);

        selection.Ids.Should().Equal(B);
        selection.SingleId.Should().Be(B);
        selection.Anchor.Should().Be(B);
    }

    [Fact]
    public void Ctrl_click_adds_then_removes_a_row()
    {
        var selection = TreeSelection.Empty.SelectOnly(A).Toggle(B);

        selection.Ids.Should().BeEquivalentTo(new[] { A, B });
        selection.SingleId.Should().BeNull("more than one row is selected");

        selection.Toggle(B).Ids.Should().Equal(A);
    }

    [Fact]
    public void Shift_click_selects_the_range_between_anchor_and_target()
    {
        var rows = Rows(A, B, C);

        var selection = TreeSelection.Empty.SelectOnly(A).ExtendTo(rows, C);

        selection.Ids.Should().BeEquivalentTo(new[] { A, B, C });
        selection.Anchor.Should().Be(A, "the anchor stays put so the range can be resized");
    }

    [Fact]
    public void Shift_click_works_upwards_and_replaces_the_previous_range()
    {
        var rows = Rows(A, B, C);

        var selection = TreeSelection.Empty.SelectOnly(C).ExtendTo(rows, B).ExtendTo(rows, C);

        selection.Ids.Should().Equal(C);
    }

    [Fact]
    public void Shift_click_without_an_anchor_selects_just_the_target()
    {
        var selection = TreeSelection.Empty.ExtendTo(Rows(A, B), B);

        selection.Ids.Should().Equal(B);
    }

    [Fact]
    public void Retain_drops_rows_that_no_longer_exist()
    {
        var selection = TreeSelection.Empty.SelectOnly(A).Toggle(B).Retain(new[] { A });

        selection.Ids.Should().Equal(A);
        selection.Anchor.Should().BeNull("the anchor was removed along with its row");
    }
}
