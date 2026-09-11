namespace KitchenFlow.Api.Models
{
    public class Machine //Machine 이라는 클래스 정의 ,데이터 베이스의 테이블이됨
    {
        public int Id { get; set; } // 정수형 id 속성 id는 기본키이자 자동 증가값 인식
        public string Name { get; set; } = string.Empty; //empty 붙인 이유는 CS8618 경고 방지
    }
}
