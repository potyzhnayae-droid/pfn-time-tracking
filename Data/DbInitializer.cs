using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Models;

namespace PfnTimeTracking.Data;

public static class DbInitializer
{
    private const int WorkHistoryMonths = 10;

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (!await context.Departments.AnyAsync())
        {
            SeedDepartmentsAndEmployees(context);
            SeedRussianHolidays(context);
            await context.SaveChangesAsync();
            await SeedIdentityUsersAsync(provider, context);
        }

        if (!await context.WorkDays.AnyAsync())
            await SeedWorkDaysAndOvertimeAsync(context);

        if (!await context.CorrectionRequests.AnyAsync())
            await SeedSampleCorrectionRequestsAsync(provider, context);

        await context.SaveChangesAsync();
    }

    private static void SeedDepartmentsAndEmployees(ApplicationDbContext context)
    {
        var departments = new[]
        {
            new Department { Name = "Управление назначения пенсий" },
            new Department { Name = "Отдел сопровождения пенсионеров" },
            new Department { Name = "ИТ и автоматизация" },
            new Department { Name = "Канцелярия и архив" },
            new Department { Name = "Юридический отдел" },
            new Department { Name = "Отдел кадров" },
            new Department { Name = "Бухгалтерия" },
            new Department { Name = "Социальные выплаты" },
            new Department { Name = "Внутренний контроль" },
            new Department { Name = "Клиентская поддержка" }
        };
        context.Departments.AddRange(departments);

        var rows = new (string Name, int DeptIdx, string Pos, WorkScheduleType Sched, decimal Rate)[]
        {
            ("Иванов Иван Иванович", 0, "Главный специалист", WorkScheduleType.FiveDayWeek, 520),
            ("Петрова Мария Сергеевна", 2, "Ведущий инженер", WorkScheduleType.Flexible, 610),
            ("Сидоров Пётр Александрович", 3, "Делопроизводитель", WorkScheduleType.Summarized, 410),
            ("Козлова Анна Викторовна", 1, "Специалист 1 кат.", WorkScheduleType.FiveDayWeek, 480),
            ("Морозов Дмитрий Олегович", 4, "Юрисконсульт", WorkScheduleType.FiveDayWeek, 590),
            ("Волкова Елена Николаевна", 5, "Инспектор по кадрам", WorkScheduleType.FiveDayWeek, 470),
            ("Новиков Андрей Игоревич", 6, "Бухгалтер", WorkScheduleType.FiveDayWeek, 500),
            ("Соколова Ольга Павловна", 7, "Экономист", WorkScheduleType.FiveDayWeek, 490),
            ("Лебедев Константин Сергеевич", 8, "Аудитор", WorkScheduleType.Flexible, 640),
            ("Кузнецова Татьяна Михайловна", 9, "Оператор кол-центра", WorkScheduleType.Shift, 430),
            ("Попов Артём Владимирович", 0, "Специалист", WorkScheduleType.FiveDayWeek, 460),
            ("Васильева Наталья Андреевна", 1, "Консультант", WorkScheduleType.FiveDayWeek, 455),
            ("Семёнов Роман Денисович", 2, "Системный администратор", WorkScheduleType.Summarized, 580),
            ("Егорова Дарья Станиславовна", 3, "Архивариус", WorkScheduleType.FiveDayWeek, 400),
            ("Павлов Максим Юрьевич", 4, "Старший юрисконсульт", WorkScheduleType.FiveDayWeek, 670),
            ("Степанова Ирина Валерьевна", 5, "Специалист по обучению", WorkScheduleType.FiveDayWeek, 475),
            ("Николаев Сергей Петрович", 6, "Главный бухгалтер", WorkScheduleType.FiveDayWeek, 720),
            ("Орлова Ксения Дмитриевна", 7, "Специалист по выплатам", WorkScheduleType.FiveDayWeek, 485),
            ("Андреев Виктор Семёнович", 8, "Контролёр", WorkScheduleType.Flexible, 540),
            ("Макарова Юлия Олеговна", 9, "Старший оператор", WorkScheduleType.Shift, 450),
            ("Захаров Илья Константинович", 0, "Начальник сектора", WorkScheduleType.FiveDayWeek, 690),
            ("Белова Светлана Игоревна", 1, "Ведущий специалист", WorkScheduleType.FiveDayWeek, 510),
            ("Комаров Никита Романович", 2, "Разработчик", WorkScheduleType.Flexible, 620),
            ("Григорьева Алёна Павловна", 3, "Секретарь", WorkScheduleType.FiveDayWeek, 420),
            ("Титов Борис Анатольевич", 4, "Зам. начальника отдела", WorkScheduleType.FiveDayWeek, 710),
            ("Романова Марина Сергеевна", 5, "Делопроизводитель", WorkScheduleType.Summarized, 405),
            ("Фёдоров Глеб Васильевич", 6, "Экономист по труду", WorkScheduleType.FiveDayWeek, 495),
            ("Дмитриева Вероника Алексеевна", 7, "Специалист", WorkScheduleType.FiveDayWeek, 470),
            ("Жуков Павел Евгеньевич", 8, "Аналитик", WorkScheduleType.Flexible, 560),
            ("Тихонова Лидия Николаевна", 9, "Руководитель смены", WorkScheduleType.Shift, 520),
            ("Баранов Станислав Олегович", 0, "Специалист 2 кат.", WorkScheduleType.FiveDayWeek, 440),
            ("Гусева Полина Денисовна", 1, "Младший специалист", WorkScheduleType.FiveDayWeek, 390),
            ("Крылов Арсений Максимович", 2, "Инженер по сетям", WorkScheduleType.Summarized, 550),
            ("Тарасова Екатерина Владимировна", 3, "Старший делопроизводитель", WorkScheduleType.FiveDayWeek, 430),
            ("Белов Денис Сергеевич", 4, "Помощник юриста", WorkScheduleType.FiveDayWeek, 420),
            ("Миронова Алиса Игоревна", 5, "Рекрутер", WorkScheduleType.Flexible, 500),
            ("Киселёв Олег Антонович", 6, "Старший бухгалтер", WorkScheduleType.FiveDayWeek, 580),
            ("Афанасьева Надежда Павловна", 7, "Специалист по ЕДЦ", WorkScheduleType.FiveDayWeek, 475),
            ("Мельников Тимофей Русланович", 8, "Ревизор", WorkScheduleType.FiveDayWeek, 530),
            ("Калинина Виктория Олеговна", 9, "Оператор", WorkScheduleType.Shift, 410),
            ("Сорокин Владислав Игоревич", 0, "Специалист", WorkScheduleType.FiveDayWeek, 450),
            ("Воронцова Ангелина Сергеевна", 2, "Тестировщик", WorkScheduleType.Flexible, 540),
            ("Герасимов Руслан Тимурович", 4, "Юрист", WorkScheduleType.FiveDayWeek, 505),
            ("Медведева Инна Валерьевна", 5, "Кадровый специалист", WorkScheduleType.FiveDayWeek, 465),
            ("Кудрявцев Артур Викторович", 6, "Финансовый контролёр", WorkScheduleType.FiveDayWeek, 600),
            ("Сафонова Лариса Михайловна", 7, "Ведущий экономист", WorkScheduleType.FiveDayWeek, 515),
            ("Рябов Евгений Николаевич", 8, "Специалист", WorkScheduleType.Summarized, 445),
            ("Шестакова Оксана Дмитриевна", 9, "Супервайзер", WorkScheduleType.Shift, 480),
            ("Фомин Григорий Алексеевич", 1, "Консультант 1 линии", WorkScheduleType.FiveDayWeek, 435),
            ("Денисова Карина Романовна", 3, "Оператор документооборота", WorkScheduleType.FiveDayWeek, 415)
        };

        foreach (var r in rows)
        {
            context.Employees.Add(new Employee
            {
                FullName = r.Name,
                Department = departments[r.DeptIdx],
                Position = r.Pos,
                WorkScheduleType = r.Sched,
                HourlyRate = r.Rate
            });
        }
    }

    private static void SeedRussianHolidays(ApplicationDbContext context)
    {
        var years = new HashSet<int>();
        var today = DateTime.Today;
        for (var i = 0; i <= WorkHistoryMonths + 1; i++)
            years.Add(today.AddMonths(-i).Year);

        foreach (var year in years)
        {
            foreach (var d in GetRussianHolidayDates(year))
            {
                context.WorkScheduleExceptions.Add(new WorkScheduleException
                {
                    ExceptionDate = d,
                    IsHoliday = true,
                    Description = "Праздничный день"
                });
            }
        }
    }

    private static IEnumerable<DateTime> GetRussianHolidayDates(int year)
    {
        for (var m = 1; m <= 8; m++)
            yield return new DateTime(year, 1, m);
        yield return new DateTime(year, 2, 23);
        yield return new DateTime(year, 3, 8);
        yield return new DateTime(year, 5, 1);
        yield return new DateTime(year, 5, 9);
        yield return new DateTime(year, 6, 12);
        yield return new DateTime(year, 11, 4);
    }

    private static async Task SeedIdentityUsersAsync(IServiceProvider provider, ApplicationDbContext context)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        const string adminEmail = "admin@pfn.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "Администратор системы"
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, AppRoles.Administrator);
        }

        const string accEmail = "buh@pfn.local";
        if (await userManager.FindByEmailAsync(accEmail) is null)
        {
            var acc = new ApplicationUser
            {
                UserName = accEmail,
                Email = accEmail,
                EmailConfirmed = true,
                DisplayName = "Бухгалтер"
            };
            await userManager.CreateAsync(acc, "Buh123!");
            await userManager.AddToRoleAsync(acc, AppRoles.Accountant);
        }

        const string hrEmail = "hr@pfn.local";
        if (await userManager.FindByEmailAsync(hrEmail) is null)
        {
            var hr = new ApplicationUser
            {
                UserName = hrEmail,
                Email = hrEmail,
                EmailConfirmed = true,
                DisplayName = "Кадровик"
            };
            await userManager.CreateAsync(hr, "Hr123!");
            await userManager.AddToRoleAsync(hr, AppRoles.HR);
        }

        var firstEmployee = await context.Employees.OrderBy(e => e.Id).FirstAsync();
        var empUserEmail = "employee@pfn.local";
        if (await userManager.FindByEmailAsync(empUserEmail) is null)
        {
            var empUser = new ApplicationUser
            {
                UserName = empUserEmail,
                Email = empUserEmail,
                EmailConfirmed = true,
                DisplayName = firstEmployee.FullName,
                EmployeeId = firstEmployee.Id
            };
            await userManager.CreateAsync(empUser, "User123!");
            await userManager.AddToRoleAsync(empUser, AppRoles.Employee);
        }

        var headDept = await context.Departments.OrderBy(d => d.Id).Skip(2).FirstAsync();
        var headEmail = "head@pfn.local";
        if (await userManager.FindByEmailAsync(headEmail) is null)
        {
            var headUser = new ApplicationUser
            {
                UserName = headEmail,
                Email = headEmail,
                EmailConfirmed = true,
                DisplayName = "Руководитель ИТ",
                ManagedDepartmentId = headDept.Id
            };
            await userManager.CreateAsync(headUser, "Head123!");
            await userManager.AddToRoleAsync(headUser, AppRoles.DepartmentHead);
        }
    }

    private static async Task SeedWorkDaysAndOvertimeAsync(ApplicationDbContext context)
    {
        var employees = await context.Employees.AsNoTracking().ToListAsync();
        var holidays = (await context.WorkScheduleExceptions.AsNoTracking()
                .Where(x => x.IsHoliday)
                .Select(x => x.ExceptionDate)
                .ToListAsync())
            .Select(d => d.Date)
            .ToHashSet();

        var rng = new Random(20260427);
        var end = DateTime.Today;
        var start = end.AddMonths(-WorkHistoryMonths);

        foreach (var emp in employees)
        {
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                if (!IsWorkingDayForSeed(day, holidays, emp.WorkScheduleType))
                    continue;

                if (rng.NextDouble() < 0.035)
                {
                    context.WorkDays.Add(CreateAbsentDay(emp.Id, day, rng));
                    continue;
                }

                var (startH, startM, endH, endM, lunch) = GetTimesForSchedule(emp, rng);
                var date = day.Date;
                var wd = new WorkDay
                {
                    EmployeeId = emp.Id,
                    Date = date,
                    StartTime = date.AddHours(startH).AddMinutes(startM),
                    EndTime = date.AddHours(endH).AddMinutes(endM),
                    LunchBreakMinutes = lunch,
                    IsAbsent = false
                };

                if (rng.NextDouble() < 0.072)
                {
                    wd.EndTime = wd.EndTime!.Value.AddHours(1 + rng.NextDouble() * 1.5);
                    wd.Overtime = new Overtime
                    {
                        OvertimeHours = Math.Round((decimal)(0.5 + rng.NextDouble() * 2.5), 2),
                        NightHours = rng.NextDouble() < 0.25
                            ? Math.Round((decimal)(rng.NextDouble() * 1.2), 2)
                            : 0
                    };
                }

                context.WorkDays.Add(wd);
            }
        }
    }

    private static bool IsWorkingDayForSeed(DateTime day, HashSet<DateTime> holidays, WorkScheduleType schedule)
    {
        var d = day.Date;
        if (holidays.Contains(d))
            return false;
        if (schedule == WorkScheduleType.Shift)
            return d.DayOfWeek is not DayOfWeek.Sunday;
        return d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    private static WorkDay CreateAbsentDay(int employeeId, DateTime day, Random rng)
    {
        var reasons = new[] { "Болезнь", "Отпуск", "Обучение", "Отпуск без сохранения" };
        return new WorkDay
        {
            EmployeeId = employeeId,
            Date = day.Date,
            IsAbsent = true,
            AbsenceReason = reasons[rng.Next(reasons.Length)],
            LunchBreakMinutes = 0
        };
    }

    private static (int sh, int sm, int eh, int em, int lunch) GetTimesForSchedule(Employee emp, Random rng)
    {
        return emp.WorkScheduleType switch
        {
            WorkScheduleType.Flexible when rng.NextDouble() < 0.5 => (10, 0, 19, 0, 60),
            WorkScheduleType.Flexible => (8, 30, 17, 30, 45),
            WorkScheduleType.Shift when emp.Id % 2 == 0 => (14, 0, 23, 0, 60),
            WorkScheduleType.Shift => (8, 0, 17, 0, 60),
            _ => (9, 0, 18, 0, 60)
        };
    }

    private static async Task SeedSampleCorrectionRequestsAsync(IServiceProvider provider, ApplicationDbContext context)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("employee@pfn.local");
        if (user?.EmployeeId is not int eid)
            return;

        var sampleDates = await context.WorkDays.AsNoTracking()
            .Where(w => w.EmployeeId == eid && !w.IsAbsent)
            .OrderByDescending(w => w.Date)
            .Take(4)
            .Select(w => w.Date)
            .ToListAsync();

        var messages = new[]
        {
            "Прошу скорректировать время ухода: задержался на совещании.",
            "Ошибочно указан обед, фактически 45 минут.",
            "Нужно исправить время прихода — опоздание согласовано с руководителем."
        };

        for (var i = 0; i < sampleDates.Count; i++)
        {
            context.CorrectionRequests.Add(new CorrectionRequest
            {
                EmployeeId = eid,
                UserId = user.Id,
                WorkDate = sampleDates[i],
                Message = messages[i % messages.Length],
                CreatedAt = DateTime.UtcNow.AddDays(-14 + i * 2),
                IsResolved = i % 2 == 0
            });
        }
    }
}
