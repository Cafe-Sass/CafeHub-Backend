using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu
{
    public sealed class MenuCategoryTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public void Creation_SetsAnImmutableParentAndRejectsDepthThreeOrSelfParenting()
        {
            var root = Create(" Drinks ");
            var child = Create(" Hot ", root);

            Assert.Null(root.ParentId);
            Assert.Equal("Drinks", root.Name);
            Assert.Equal(MenuCategoryStatus.Active, root.Status);
            Assert.Equal(root.Id, child.ParentId);
            Assert.Equal("Hot", child.Name);
            Assert.Throws<ArgumentException>(() => Create("Deep", child));
            Assert.Throws<ArgumentException>(() => MenuCategory.Create(root.Id, root, "Self", 0, Now, Actor));

            Assert.True(child.Update("Espresso", 4, Now.AddMinutes(1), Actor));
            Assert.Equal(root.Id, child.ParentId);
        }

        [Fact]
        public void ParentLifecycle_PreservesEachChildStateAndRestrictsNewChildActivation()
        {
            var parent = Create("Drinks");
            var active = Create("Hot", parent);
            var inactive = Create("Cold", parent);
            inactive.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor);

            Assert.True(parent.ChangeStatus(MenuCategoryStatus.Inactive, null, Now, Actor));
            Assert.Equal(MenuCategoryStatus.Active, active.Status);
            Assert.Equal(MenuCategoryStatus.Inactive, inactive.Status);
            Assert.Throws<ArgumentException>(() => Create("Fresh", parent));
            Assert.Throws<InvalidOperationException>(() => inactive.ChangeStatus(MenuCategoryStatus.Active, parent, Now, Actor));
            Assert.False(active.ChangeStatus(MenuCategoryStatus.Active, parent, Now, Actor));

            Assert.True(parent.ChangeStatus(MenuCategoryStatus.Active, null, Now, Actor));
            Assert.Equal(MenuCategoryStatus.Inactive, inactive.Status);
            Assert.True(inactive.ChangeStatus(MenuCategoryStatus.Active, parent, Now, Actor));
        }

        [Fact]
        public void Names_UseUnicodeScalarsAndPreserveTheOriginalSpelling()
        {
            var limit = string.Concat(Enumerable.Repeat("\U0001F600", 64));
            var category = Create($"\u00a0{limit}\t");
            Assert.Equal(limit, category.Name);
            Assert.Equal("Cafe\u0301", Create("Cafe\u0301").Name);
            Assert.False(MenuCategory.IsValidName(limit + "a"));
            Assert.False(MenuCategory.IsValidName(string.Concat(Enumerable.Repeat("e\u0301", 33))));
            Assert.False(MenuCategory.IsValidName("\ud800"));
            Assert.False(MenuCategory.IsValidName("Foo\0Bar"));
            Assert.False(MenuCategory.IsValidName(" \t "));
        }

        [Fact]
        public void Validation_RejectsInvalidOrderTokenAndUnrepresentablePage()
        {
            var create = new CreateMenuCategoryCommandValidator();
            Assert.True(create.Validate(new CreateMenuCategoryCommand(null, "Drinks", 0)).IsValid);
            Assert.False(create.Validate(new CreateMenuCategoryCommand(Guid.Empty, "Drinks", 0)).IsValid);
            Assert.False(create.Validate(new CreateMenuCategoryCommand(null, "Drinks", -1)).IsValid);
            Assert.False(new UpdateMenuCategoryCommandValidator()
                .Validate(new UpdateMenuCategoryCommand(Guid.NewGuid(), "Drinks", 0, "4294967296")).IsValid);
            Assert.False(new GetMenuCategoryTreeQueryValidator(new PaginationOptions())
                .Validate(new GetMenuCategoryTreeQuery(false, new OffsetPageRequest { Page = int.MaxValue })).IsValid);
        }

        private static MenuCategory Create(string name, MenuCategory? parent = null) =>
            MenuCategory.Create(Guid.CreateVersion7(), parent, name, 0, Now, Actor);
    }
}
