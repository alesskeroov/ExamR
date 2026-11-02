using System.ComponentModel.DataAnnotations;
using ExamFlow.Api.Features.Auth;

namespace ExamFlow.Api.Tests;

public class ProfileContractTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("<script>alert('text')</script>", true)]
    public void ProfileAcceptsOptionalPlainTextBio(string? bio, bool expected)
    {
        var request = new UpdateProfileRequest { Nickname = "student_1", Bio = bio };
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Theory]
    [InlineData(160, true)]
    [InlineData(161, false)]
    public void ProfileEnforcesBioLength(int length, bool expected)
    {
        var request = new UpdateProfileRequest { Nickname = "student_1", Bio = new string('a', length) };
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
