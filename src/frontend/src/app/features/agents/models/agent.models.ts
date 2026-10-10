export interface AgentDto {userId:string;displayName:string;email:string;role:'Agent';isActive:boolean;assignmentReferenceId:string;version:string;}
export interface AgentPage {items:AgentDto[];page:number;pageSize:number;totalCount:number;}
