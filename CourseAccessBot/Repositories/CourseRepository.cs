using CourseAccessBot.Models;
using System.Text.Json;

namespace CourseAccessBot.Repositories
{
    public class CourseRepository
    {
        private readonly string _coursesFilePath;
        private List<Course> _courses;

        public CourseRepository(string coursesFilePath)
        {
            _coursesFilePath = coursesFilePath;
            _courses = new List<Course>();
            LoadCourses();
        }

        private void LoadCourses()
        {
            if (File.Exists(_coursesFilePath))
            {
                var json = File.ReadAllText(_coursesFilePath);
                var courses = JsonSerializer.Deserialize<List<Course>>(json);
                if (courses != null)
                    _courses = courses;
            }
            else
            {
                // Если файла нет, создаём пустой и сохраняем
                _courses = new List<Course>();
                SaveCourses();
            }
        }

        private void SaveCourses()
        {
            var json = JsonSerializer.Serialize(_courses, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_coursesFilePath, json);
        }

        public IEnumerable<Course> GetAllCourses() => _courses;

        public Course? GetCourseById(int id) => _courses.FirstOrDefault(c => c.Id == id);

        public void AddCourse(Course course)
        {
            // Генерируем Id
            if (_courses.Any())
                course.Id = _courses.Max(c => c.Id) + 1;
            else
                course.Id = 1;

            _courses.Add(course);
            SaveCourses();
        }

        public bool RemoveCourse(int courseId)
        {
            var course = GetCourseById(courseId);
            if (course != null)
            {
                _courses.Remove(course);
                SaveCourses();
                return true;
            }
            return false;
        }
    }
}
