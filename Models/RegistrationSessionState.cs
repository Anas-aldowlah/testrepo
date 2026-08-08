namespace YAGOT_2._0.Models
{
    public class RegistrationSessionState
    {
        public string? GoogleSubjectId { get; set; }
        public string? GoogleEmail { get; set; }
        public string? GoogleName { get; set; }
        public string? GooglePicture { get; set; }
        public bool IsGoogleVerified { get; set; }

        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsPhoneStepCompleted { get; set; }

        public int Step { get; set; } = 1;
    }
}
